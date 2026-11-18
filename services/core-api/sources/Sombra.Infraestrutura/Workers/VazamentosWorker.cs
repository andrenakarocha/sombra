using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;

namespace Sombra.Infraestrutura.Workers;

/// <summary>
/// Registra os vazamentos bloqueados que os guardas enfileiraram: <c>privacidade.vazamento_bloqueado</c> (log, resposta,
/// exceção) e <c>privacidade.saida_externa_bloqueada</c> (HTTP para fora e MQTT), com a correlação de origem.
/// </summary>
public sealed partial class VazamentosWorker : BackgroundService
{
    private readonly FilaDeVazamentos _fila;
    private readonly ILogger<VazamentosWorker> _log;

    /// <summary>Initializes a new instance of the <see cref="VazamentosWorker"/> class.</summary>
    /// <param name="fila">A fila dos guardas.</param>
    /// <param name="log">O log.</param>
    public VazamentosWorker(FilaDeVazamentos fila, ILogger<VazamentosWorker> log)
    {
        _fila = fila ?? throw new ArgumentNullException(nameof(fila));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (VazamentoBloqueado evento in _fila.Leitor.ReadAllAsync(stoppingToken))
        {
            using IDisposable? escopo = _log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = evento.CorrelationId ?? "-" });
            if (evento.Sink is SinkDePii.SaidaExterna or SinkDePii.Mqtt)
            {
                SaidaExternaBloqueada(_log, evento.Sink, evento.Onde, evento.Categoria, evento.RegraId);
            }
            else
            {
                VazamentoBloqueado(_log, evento.Sink, evento.Onde, evento.Categoria, evento.RegraId, evento.PeloTaint ? "taint" : evento.RegraId == "acesso-negado" ? "controle de acesso" : "conteudo");
            }
        }
    }

    [LoggerMessage(EventId = 4401, EventName = "privacidade.vazamento_bloqueado", Level = LogLevel.Warning,
        Message = "Vazamento bloqueado no sink {sink} em {onde}: {categoria} (regra {regra}, pelo {deteccao})")]
    private static partial void VazamentoBloqueado(ILogger logger, SinkDePii sink, string onde, CategoriaDePii categoria, string regra, string deteccao);

    [LoggerMessage(EventId = 4402, EventName = "privacidade.saida_externa_bloqueada", Level = LogLevel.Warning,
        Message = "Saída {sink} para {onde} bloqueada: {categoria} (regra {regra})")]
    private static partial void SaidaExternaBloqueada(ILogger logger, SinkDePii sink, string onde, CategoriaDePii categoria, string regra);
}
