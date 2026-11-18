using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Serilog.Core;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Dtos;
using Testcontainers.PostgreSql;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>
/// Um PostgreSQL 16 e um Mosquitto 2 de verdade para a montagem inteira; cada fábrica da API ganha um banco e um prefixo
/// de tópicos só dela.
/// </summary>
[TestClass]
public sealed class AmbienteDaApi
{
    private static PostgreSqlContainer? _postgres;
    private static IContainer? _mosquitto;

    /// <summary>Gets o host do broker.</summary>
    internal static string MqttHost => _mosquitto!.Hostname;

    /// <summary>Gets a porta do broker.</summary>
    internal static int MqttPorta => _mosquitto!.GetMappedPublicPort(1883);

    /// <summary>Gets o JSON do contrato: camelCase e enums em SNAKE_CASE maiúsculo.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    /// <summary>Sobe o contêiner.</summary>
    /// <param name="contexto">O contexto do MSTest.</param>
    /// <returns>A subida.</returns>
    [AssemblyInitialize]
    public static async Task IniciarAsync(TestContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        _mosquitto = new ContainerBuilder("eclipse-mosquitto:2")
            .WithCommand("mosquitto", "-c", "/mosquitto-no-auth.conf")
            .WithPortBinding(1883, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("mosquitto version .* running"))
            .Build();
        await Task.WhenAll(_postgres.StartAsync(contexto.CancellationToken), _mosquitto.StartAsync(contexto.CancellationToken));
    }

    /// <summary>Derruba o contêiner.</summary>
    /// <returns>A descida.</returns>
    [AssemblyCleanup]
    public static async Task EncerrarAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_mosquitto is not null)
        {
            await _mosquitto.DisposeAsync();
        }
    }

    /// <summary>Uma conexão para um banco novo (a API o cria e migra ao subir).</summary>
    /// <returns>A string de conexão.</returns>
    internal static string NovaConexao()
        => new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString()) { Database = $"api_{Guid.NewGuid():N}" }.ConnectionString;
}

/// <summary>
/// A Core API de verdade em memória, com banco próprio, migrações, seed e contas da demo. O segredo do JWT e a senha
/// da demo são gerados na hora: nada de credencial no código.
/// </summary>
internal sealed class FabricaDaApi : WebApplicationFactory<Program>
{
    private readonly string _conexao = AmbienteDaApi.NovaConexao();
    private readonly string _segredoDoJwt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly int _loginPorMinuto;
    private readonly ILogEventSink? _log;
    private readonly IReadOnlyDictionary<string, string>? _configuracao;
    private readonly Action<IServiceCollection>? _servicos;

    /// <summary>Initializes a new instance of the <see cref="FabricaDaApi"/> class.</summary>
    /// <param name="loginPorMinuto">O limite de logins por minuto.</param>
    /// <param name="log">Um sink extra para capturar o log, quando o teste confere as linhas.</param>
    /// <param name="configuracao">Configuração a mais (ex.: workers mais rápidos).</param>
    /// <param name="servicos">Troca de serviços (ex.: um relógio controlado pelo teste).</param>
    internal FabricaDaApi(int loginPorMinuto = 1000, ILogEventSink? log = null, IReadOnlyDictionary<string, string>? configuracao = null, Action<IServiceCollection>? servicos = null)
    {
        _loginPorMinuto = loginPorMinuto;
        _log = log;
        _configuracao = configuracao;
        _servicos = servicos;
    }

    /// <summary>Gets o prefixo dos tópicos MQTT desta API (cada fábrica tem o seu, no mesmo broker).</summary>
    internal string Prefixo { get; } = $"sombra-teste/{Guid.NewGuid():N}";

    /// <summary>Gets a senha das contas da demo neste teste.</summary>
    internal string SenhaDaDemo { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

    /// <summary>Um cliente já autenticado com uma conta da demo.</summary>
    /// <param name="email">O e-mail da conta.</param>
    /// <returns>O cliente com o header <c>Authorization</c>.</returns>
    internal async Task<HttpClient> ClienteDeAsync(string email)
    {
        HttpClient cliente = CreateClient();
        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Email = email, Senha = SenhaDaDemo }, AmbienteDaApi.Json);
        resposta.EnsureSuccessStatusCode();
        TokenResposta? token = await resposta.Content.ReadFromJsonAsync<TokenResposta>(AmbienteDaApi.Json);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return cliente;
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Test");
        builder.UseSetting("ConnectionStrings:Core", _conexao);
        builder.UseSetting("Jwt:Segredo", _segredoDoJwt);
        builder.UseSetting("Demo:Senha", SenhaDaDemo);
        builder.UseSetting("Banco:MigrarAoIniciar", "true");
        builder.UseSetting("LimiteDeRequisicoes:LoginPorMinuto", _loginPorMinuto.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Versao:Commit", "teste01");
        builder.UseSetting("Mqtt:Host", AmbienteDaApi.MqttHost);
        builder.UseSetting("Mqtt:Porta", AmbienteDaApi.MqttPorta.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Mqtt:PrefixoTopico", Prefixo);
        foreach ((string chave, string valor) in _configuracao ?? new Dictionary<string, string>())
        {
            builder.UseSetting(chave, valor);
        }

        builder.ConfigureTestServices(servicos =>
        {
            if (_log is not null)
            {
                servicos.AddSingleton(_log);
            }

            _servicos?.Invoke(servicos);
        });
    }

    /// <summary>Espera o Core ficar pronto (banco e broker).</summary>
    /// <returns>A espera.</returns>
    internal async Task EsperarProntoAsync()
    {
        using HttpClient cliente = CreateClient();
        await Esperar.AteAsync(async () => (await cliente.GetAsync(new Uri("/health/ready", UriKind.Relative))).IsSuccessStatusCode);
    }
}

/// <summary>As contas e as casas da demonstração.</summary>
internal static class Demo
{
    /// <summary>A filha da Dona Lurdes (Cuidador).</summary>
    internal const string Ana = "ana.filha@demo.sombra";

    /// <summary>O filho do Seu Joaquim (Cuidador).</summary>
    internal const string Marcos = "marcos.filho@demo.sombra";

    /// <summary>A operação (Operador).</summary>
    internal const string Operador = "operador@demo.sombra";

    /// <summary>A administração (Admin).</summary>
    internal const string Admin = "admin@demo.sombra";

    /// <summary>A casa da Dona Lurdes.</summary>
    internal static readonly Guid Lurdes = new("0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10");

    /// <summary>A casa do Seu Joaquim.</summary>
    internal static readonly Guid Joaquim = new("4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31");
}

/// <summary>Espera uma condição assíncrona, sem dormir à toa.</summary>
internal static class Esperar
{
    /// <summary>Repete a conferência a cada 100 ms até ela passar ou o tempo acabar.</summary>
    /// <param name="condicao">A condição.</param>
    /// <param name="segundos">O limite.</param>
    /// <returns>A espera.</returns>
    internal static async Task AteAsync(Func<Task<bool>> condicao, double segundos = 15)
    {
        DateTime limite = DateTime.UtcNow.AddSeconds(segundos);
        while (!await condicao())
        {
            Assert.IsLessThan(limite, DateTime.UtcNow, "A condição não aconteceu a tempo.");
            await Task.Delay(100);
        }
    }

    /// <summary>Espera uma condição síncrona.</summary>
    /// <param name="condicao">A condição.</param>
    /// <param name="segundos">O limite.</param>
    /// <returns>A espera.</returns>
    internal static Task AteAsync(Func<bool> condicao, double segundos = 15) => AteAsync(() => Task.FromResult(condicao()), segundos);
}
