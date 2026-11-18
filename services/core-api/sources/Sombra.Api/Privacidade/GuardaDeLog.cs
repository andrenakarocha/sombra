using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Sombra.Api.Composicao;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Api.Privacidade;

/// <summary>
/// O guarda do sink de log (spec 07 §2.1): decora os sinks do Serilog, então todo evento passa por aqui antes de ser
/// escrito. Cada propriedade, o texto do template e a exceção são conferidos contra o taint da requisição e o motor de
/// PII; o que tiver dado pessoal é redigido (<c>[PII:regra]</c>) e o vazamento vai para a fila. Cobre o
/// <c>logger.info(dados_de_saude)</c> do enunciado.
/// </summary>
internal sealed class GuardaDeLog : ILogEventSink
{
    // Propriedades que o próprio Core preenche e nunca carregam dado de entrada.
    private static readonly HashSet<string> _seguras = new(StringComparer.Ordinal)
    {
        "Version", "Environment", LogDoSombra.CorrelationId, "EventId", "SourceContext",
    };

    private readonly ILogEventSink _destino;
    private readonly GuardaDeSaida _guarda;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ModoDaPrivacidade _modo;

    /// <summary>Initializes a new instance of the <see cref="GuardaDeLog"/> class.</summary>
    /// <param name="destino">Os sinks de verdade (console e os de teste).</param>
    /// <param name="guarda">A guarda de saída.</param>
    /// <param name="observador">Quem registra o bloqueio.</param>
    /// <param name="modo">Se redige ou só registra (modo inseguro da auditoria).</param>
    public GuardaDeLog(ILogEventSink destino, GuardaDeSaida guarda, IObservadorDeVazamentos observador, ModoDaPrivacidade modo)
    {
        _destino = destino;
        _guarda = guarda;
        _observador = observador;
        _modo = modo;
    }

    /// <inheritdoc/>
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ContextoDeTaint? taint = ContextoDeTaint.Atual;
        List<AchadoNaSaida> achados = [];

        List<LogEventProperty> propriedades = [];
        foreach ((string nome, LogEventPropertyValue valor) in logEvent.Properties)
        {
            ResultadoDaGuarda r = _seguras.Contains(nome) ? new([], string.Empty) : _guarda.Verificar(Texto(valor), taint, nome);
            achados.AddRange(r.Achados);
            propriedades.Add(new LogEventProperty(nome, r.Limpo ? valor : new ScalarValue(r.Redigido)));
        }

        ResultadoDaGuarda template = _guarda.Verificar(logEvent.MessageTemplate.Text, taint);
        achados.AddRange(template.Achados);
        ResultadoDaGuarda excecao = _guarda.Verificar(logEvent.Exception?.ToString(), taint);
        achados.AddRange(excecao.Achados);

        if (achados.Count == 0 || !_modo.Bloqueia)
        {
            _destino.Emit(logEvent);
            if (achados.Count == 0)
            {
                return;
            }
        }
        else
        {
            _destino.Emit(new LogEvent(
                logEvent.Timestamp,
                logEvent.Level,
                excecao.Limpo ? logEvent.Exception : new ExcecaoRedigida(logEvent.Exception!.GetType().FullName ?? "Exception", excecao.Redigido),
                template.Limpo ? logEvent.MessageTemplate : new MessageTemplateParser().Parse(template.Redigido),
                propriedades));
        }

        string onde = (logEvent.Properties.GetValueOrDefault("EventId") as StructureValue)?.Properties
            .FirstOrDefault(p => p.Name == "Name")?.Value is ScalarValue { Value: string evento } ? evento : "log";
        string? correlacao = logEvent.Properties.GetValueOrDefault(LogDoSombra.CorrelationId) is ScalarValue { Value: string c } ? c : null;
        foreach (AchadoNaSaida a in achados.Distinct())
        {
            _observador.Registrar(new VazamentoBloqueado(SinkDePii.Log, onde, a.RegraId, a.Categoria, a.Severidade, a.PeloTaint, correlacao, Bloqueado: _modo.Bloqueia));
        }
    }

    private static string? Texto(LogEventPropertyValue valor) => valor switch
    {
        ScalarValue { Value: null } => null,
        ScalarValue { Value: string s } => s,
        ScalarValue { Value: IFormattable f } => f.ToString(null, CultureInfo.InvariantCulture),
        ScalarValue s => s.Value?.ToString(),
        _ => valor.ToString(),
    };

    /// <summary>A exceção como vai para o log depois de redigida: o tipo original e o texto sem PII.</summary>
    private sealed class ExcecaoRedigida : Exception
    {
        private readonly string _tipo;

        public ExcecaoRedigida(string tipo, string texto)
            : base(texto) => _tipo = tipo;

        public override string ToString() => $"{_tipo} (redigida pelo Sombra.Privacidade): {Message}";
    }
}
