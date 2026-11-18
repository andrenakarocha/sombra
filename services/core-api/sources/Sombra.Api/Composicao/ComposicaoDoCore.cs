using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Sombra.Api.Autenticacao;
using Sombra.Api.Erros;
using Sombra.Api.TempoReal;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura;
using Sombra.Infraestrutura.Autenticacao;
using Sombra.Infraestrutura.Mqtt;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Api.Composicao;

/// <summary>Monta a Core API: serviços, segurança, erros, limites, saúde e documentação.</summary>
public static class ComposicaoDoCore
{
    /// <summary>A política de limite do login (por IP), mais apertada contra força bruta.</summary>
    public const string LimiteDoLogin = "login";

    /// <summary>A tag dos health checks que dizem se o Core está pronto (banco e broker MQTT).</summary>
    public const string TagPronto = "pronto";

    /// <summary>Registra os serviços.</summary>
    /// <param name="builder">O construtor da aplicação.</param>
    /// <exception cref="InvalidOperationException">Sem a conexão do banco ou sem a chave do JWT.</exception>
    public static void AdicionarCore(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IConfiguration configuracao = builder.Configuration;
        IServiceCollection servicos = builder.Services;
        string conexao = configuracao.GetConnectionString("Core")
            ?? throw new InvalidOperationException("Defina ConnectionStrings__Core (o compose monta a partir do .env).");
        LogDoSombra.Adicionar(builder, VersaoDoBuild.De(configuracao).Commit);

        servicos.AdicionarInfraestrutura(conexao);
        servicos.AdicionarMensageria();
        servicos.AddSingleton<ITempoReal, TempoRealSignalR>();
        servicos.AddScoped<CadastroDeDomicilios>();
        servicos.AddScoped<RegistrarLeitura>();
        servicos.AddScoped<RegistrarCheckIn>();
        servicos.AddScoped<ReconhecerAlerta>();
        servicos.AddScoped<EscalonarAlertas>();
        servicos.AddScoped<LembrarRotina>();
        servicos.AddScoped<AvaliarPrevisoes>();
        servicos.AddSignalR().AddJsonProtocol(json => ConfigurarJson(json.PayloadSerializerOptions));

        servicos.AddControllers(opcoes =>
            {
                opcoes.ModelBinderProviders.Insert(0, new ProvedorDoBinderDeEnumeracoes());
                opcoes.OutputFormatters.RemoveType<StringOutputFormatter>();
                opcoes.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
            })
            .AddJsonOptions(json => ConfigurarJson(json.JsonSerializerOptions));
        servicos.ConfigureHttpJsonOptions(json => ConfigurarJson(json.SerializerOptions));

        servicos.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            contexto.ProblemDetails.Type ??= TradutorDeExcecoes.Tipo;
            contexto.ProblemDetails.Extensions["correlationId"] = contexto.HttpContext.TraceIdentifier;
        });
        servicos.AddExceptionHandler<TradutorDeExcecoes>();

        AdicionarSeguranca(servicos, configuracao);
        AdicionarLimites(servicos, configuracao);
        servicos.AddHealthChecks()
            .AddDbContextCheck<SombraDbContext>("banco", tags: [TagPronto])
            .AddCheck<VerificacaoDoBroker>("broker", tags: [TagPronto]);
        servicos.AddOpenApi("v1", opcoes =>
        {
            opcoes.AddDocumentTransformer<DocumentoDoSombra>();
            opcoes.AddOperationTransformer<RespostasDeSeguranca>();
        });
    }

    /// <summary>Aplica as migrações (quando <c>Banco:MigrarAoIniciar</c>) e cria as contas da demo (quando há <c>Demo:Senha</c>).</summary>
    /// <param name="app">A aplicação.</param>
    /// <returns>A preparação.</returns>
    public static async Task PrepararBancoAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        bool migrar = app.Configuration.GetValue<bool>("Banco:MigrarAoIniciar");
        string? senhaDaDemo = app.Configuration["Demo:Senha"];
        if (!migrar && string.IsNullOrEmpty(senhaDaDemo) && string.IsNullOrEmpty(app.Configuration["Demo:SentinelaSegredoHex"]))
        {
            return;
        }

        await using AsyncServiceScope escopo = app.Services.CreateAsyncScope();
        SombraDbContext db = escopo.ServiceProvider.GetRequiredService<SombraDbContext>();
        if (migrar)
        {
            await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
        }

        if (!string.IsNullOrEmpty(senhaDaDemo))
        {
            await UsuariosDemo.SemearAsync(db, senhaDaDemo, app.Lifetime.ApplicationStopping);
        }

        if (app.Configuration["Demo:SentinelaDeviceId"] is { Length: > 0 } sentinela && app.Configuration["Demo:SentinelaSegredoHex"] is { Length: > 0 } segredo)
        {
            await SentinelaDaDemo.ConfigurarAsync(db, sentinela, segredo, app.Lifetime.ApplicationStopping);
        }
    }

    /// <summary>Monta o pipeline HTTP.</summary>
    /// <param name="app">A aplicação.</param>
    public static void UsarCore(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        RegistrarCicloDeVida(app);
        app.UseMiddleware<RastroDaRequisicao>();
        app.UseExceptionHandler(new ExceptionHandlerOptions { SuppressDiagnosticsCallback = _ => true });
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<HubDoSombra>(HubDoSombra.Rota);
        app.MapOpenApi().DisableRateLimiting();
        app.MapScalarApiReference(opcoes => opcoes.WithTitle("Sombra · Core API")).DisableRateLimiting();
    }

    private static void RegistrarCicloDeVida(WebApplication app)
    {
        ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Sombra.Api");
        IConfiguration configuracao = app.Configuration;
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            if (!log.IsEnabled(LogLevel.Information))
            {
                return;
            }

            string versao = VersaoDoBuild.De(configuracao).Commit;
            bool migrar = configuracao.GetValue<bool>("Banco:MigrarAoIniciar");
            bool contasDaDemo = !string.IsNullOrEmpty(configuracao["Demo:Senha"]);
            int porMinuto = configuracao.GetValue("LimiteDeRequisicoes:PorMinuto", 120);
            int loginPorMinuto = configuracao.GetValue("LimiteDeRequisicoes:LoginPorMinuto", 5);
            string emissor = configuracao["Jwt:Emissor"] ?? "sombra-core";
            log.ServicoIniciado(versao, app.Environment.EnvironmentName, migrar, contasDaDemo, porMinuto, loginPorMinuto, emissor);
        });
        app.Lifetime.ApplicationStopping.Register(log.ServicoEncerrado);
    }

    private static void ConfigurarJson(JsonSerializerOptions json)
    {
        json.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
        json.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    }

    private static void AdicionarSeguranca(IServiceCollection servicos, IConfiguration configuracao)
    {
        SymmetricSecurityKey chave = EmissorDeToken.ChaveDe(configuracao);
        servicos.AddSingleton<EmissorDeToken>();
        servicos.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opcoes =>
        {
            opcoes.MapInboundClaims = false;
            opcoes.Events = new JwtBearerEvents
            {
                OnMessageReceived = contexto =>
                {
                    if (contexto.Request.Path.StartsWithSegments(HubDoSombra.Rota, StringComparison.Ordinal)
                        && contexto.Request.Query["access_token"].ToString() is { Length: > 0 } token)
                    {
                        contexto.Token = token;
                    }

                    return Task.CompletedTask;
                },
            };
            opcoes.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = configuracao["Jwt:Emissor"] ?? "sombra-core",
                ValidAudience = EmissorDeToken.Audiencia,
                IssuerSigningKey = chave,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = JwtRegisteredClaimNames.Name,
                RoleClaimType = ClaimsDoSombra.Papel,
            };
        });
        servicos.AddAuthorizationBuilder()
            .AddPolicy(CasaDoCuidador.Politica, politica => politica.RequireAuthenticatedUser().AddRequirements(new CasaDoCuidador()));
        servicos.AddScoped<IAuthorizationHandler, CasaDoCuidadorHandler>();
    }

    private static void AdicionarLimites(IServiceCollection servicos, IConfiguration configuracao)
    {
        int porMinuto = configuracao.GetValue("LimiteDeRequisicoes:PorMinuto", 120);
        int loginPorMinuto = configuracao.GetValue("LimiteDeRequisicoes:LoginPorMinuto", 5);
        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto => RateLimitPartition.GetFixedWindowLimiter(
                QuemPede(contexto),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = porMinuto, Window = TimeSpan.FromMinutes(1) }));
            opcoes.AddPolicy(LimiteDoLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
                Ip(contexto),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
            opcoes.OnRejected = async (rejeicao, cancelamento) =>
            {
                rejeicao.HttpContext.Response.Headers.RetryAfter = "60";
                ProblemDetails problema = new()
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Muitas requisições",
                    Detail = "Espere um minuto e tente de novo.",
                };
                await rejeicao.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = rejeicao.HttpContext, ProblemDetails = problema });
            };
        });
    }

    private static string QuemPede(HttpContext contexto)
        => contexto.User.Identity?.IsAuthenticated == true
            ? string.Create(CultureInfo.InvariantCulture, $"conta:{contexto.User.Id()}")
            : Ip(contexto);

    private static string Ip(HttpContext contexto) => "ip:" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
}
