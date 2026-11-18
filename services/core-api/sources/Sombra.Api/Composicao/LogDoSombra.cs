using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Templates;
using Sombra.Api.Privacidade;
using Sombra.Privacidade.Sinks;

namespace Sombra.Api.Composicao;

/// <summary>
/// O log do Core no formato de <c>docs/observabilidade.md</c>: uma linha JSON por evento no stdout, com os oito campos
/// obrigatórios e os campos de domínio (só IDs) em camelCase.
/// </summary>
public static class LogDoSombra
{
    /// <summary>O nome do serviço nas linhas de log.</summary>
    public const string Servico = "core-api";

    /// <summary>A propriedade que o middleware de correlação abre como escopo.</summary>
    public const string CorrelationId = "CorrelationId";

    /// <summary>Gets o formato de cada linha (Serilog.Expressions).</summary>
    public static ITextFormatter Formato { get; } = new ExpressionTemplate(
        "{ {timestamp: ToString(UtcDateTime(@t), 'yyyy-MM-ddTHH:mm:ss.fffZ'), level: @l, service: '" + Servico + "', "
        + "version: Version, environment: Environment, correlationId: Coalesce(CorrelationId, '-'), "
        + "event: Coalesce(EventId.Name, 'log'), message: @m, exception: @x, ..rest()} }\n");

    /// <summary>Liga o Serilog: o stdout no formato acima e, quando houver, sinks extras do contêiner (os testes usam um).</summary>
    /// <param name="builder">O construtor da aplicação.</param>
    /// <param name="versao">O commit em execução.</param>
    public static void Adicionar(WebApplicationBuilder builder, string versao)
    {
        ArgumentNullException.ThrowIfNull(builder);
        string ambiente = builder.Environment.EnvironmentName;
        LogEventLevel minimo = builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;
        builder.Services.AddSerilog((servicos, log) =>
        {
            log.MinimumLevel.Is(minimo)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Version", versao)
                .Enrich.WithProperty("Environment", ambiente)
                .Enrich.With<SemPropriedadesDoAspNet>();

            // Todo sink passa pelo guarda de log do Sombra.Privacidade (F2.4): nenhum evento é escrito antes de ser conferido.
            GuardaDeSaida guarda = servicos.GetRequiredService<GuardaDeSaida>();
            IObservadorDeVazamentos observador = servicos.GetRequiredService<IObservadorDeVazamentos>();
            ModoDaPrivacidade modo = servicos.GetRequiredService<ModoDaPrivacidade>();
            log.WriteTo.Sink(LoggerSinkConfiguration.Wrap(
                destino => new GuardaDeLog(destino, guarda, observador, modo),
                sinks =>
                {
                    sinks.Console(Formato);
                    foreach (ILogEventSink extra in servicos.GetServices<ILogEventSink>())
                    {
                        sinks.Sink(extra);
                    }
                }));
        });
    }
}

/// <summary>Tira as propriedades que o ASP.NET Core põe em todo log (caminho, conexão, trace) e que não são do formato.</summary>
public sealed class SemPropriedadesDoAspNet : ILogEventEnricher
{
    private static readonly string[] _fora = ["SourceContext", "RequestId", "RequestPath", "ConnectionId", "SpanId", "TraceId", "ParentId", "ActionId", "ActionName"];

    /// <inheritdoc/>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (string propriedade in _fora)
        {
            logEvent.RemovePropertyIfPresent(propriedade);
        }
    }
}
