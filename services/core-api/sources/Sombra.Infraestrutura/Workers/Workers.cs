using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Mensageria;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Workers;

/// <summary>A cada 15 s: sobe os degraus vencidos (D13) e roda a rotina (R11, R12).</summary>
public sealed class EscalonadorDeAlertasWorker : WorkerPeriodico
{
    /// <summary>Initializes a new instance of the <see cref="EscalonadorDeAlertasWorker"/> class.</summary>
    /// <param name="escopos">A fábrica de escopos.</param>
    /// <param name="configuracao">A configuração (<c>Workers:EscalonadorSegundos</c>).</param>
    /// <param name="log">O log.</param>
    public EscalonadorDeAlertasWorker(IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<EscalonadorDeAlertasWorker> log)
        : base(escopos, log, TimeSpan.FromSeconds(configuracao.GetValue("Workers:EscalonadorSegundos", 15.0)), "escalonador")
    {
    }

    /// <inheritdoc/>
    protected override async Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        await servicos.GetRequiredService<EscalonarAlertas>().ExecutarAsync(cancellationToken);
        await servicos.GetRequiredService<LembrarRotina>().ExecutarAsync(cancellationToken);
    }
}

/// <summary>A cada 10 min: a previsão de 24 h de cada região e os alertas preventivos (roteiro, passo 2).</summary>
public sealed class MonitorDePrevisaoWorker : WorkerPeriodico
{
    /// <summary>Initializes a new instance of the <see cref="MonitorDePrevisaoWorker"/> class.</summary>
    /// <param name="escopos">A fábrica de escopos.</param>
    /// <param name="configuracao">A configuração (<c>Workers:PrevisaoMinutos</c>).</param>
    /// <param name="log">O log.</param>
    public MonitorDePrevisaoWorker(IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<MonitorDePrevisaoWorker> log)
        : base(escopos, log, TimeSpan.FromMinutes(configuracao.GetValue("Workers:PrevisaoMinutos", 10.0)), "previsao")
    {
    }

    /// <inheritdoc/>
    protected override Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
        => servicos.GetRequiredService<AvaliarPrevisoes>().ExecutarAsync(cancellationToken);
}

/// <summary>
/// A cada segundo: entrega os avisos pendentes do outbox a todos os canais que os atendem. Um aviso que falha fica para a
/// próxima volta, até dez tentativas.
/// </summary>
public sealed partial class PublicadorDeComandosWorker : WorkerPeriodico
{
    private readonly ILogger<PublicadorDeComandosWorker> _log;

    /// <summary>Initializes a new instance of the <see cref="PublicadorDeComandosWorker"/> class.</summary>
    /// <param name="escopos">A fábrica de escopos.</param>
    /// <param name="configuracao">A configuração (<c>Workers:OutboxSegundos</c>).</param>
    /// <param name="log">O log.</param>
    public PublicadorDeComandosWorker(IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<PublicadorDeComandosWorker> log)
        : base(escopos, log, TimeSpan.FromSeconds(configuracao.GetValue("Workers:OutboxSegundos", 1.0)), "outbox")
        => _log = log;

    /// <inheritdoc/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Um canal que falha marca o aviso para a próxima volta; os outros avisos seguem.")]
    protected override async Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        SombraDbContext db = servicos.GetRequiredService<SombraDbContext>();
        IRelogio relogio = servicos.GetRequiredService<IRelogio>();
        ICanalDeNotificacao[] canais = [.. servicos.GetServices<ICanalDeNotificacao>()];
        List<MensagemDeSaida> pendentes = await db.Outbox
            .Where(m => m.PublicadaEm == null && m.Tentativas < MensagemDeSaida.MaximoDeTentativas)
            .OrderBy(m => m.CriadaEm)
            .Take(50)
            .ToListAsync(cancellationToken);
        foreach (MensagemDeSaida mensagem in pendentes)
        {
            Correlacao.Atual = mensagem.CorrelationId;
            using IDisposable? escopoDoLog = _log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = mensagem.CorrelationId ?? "-" });
            try
            {
                foreach (ICanalDeNotificacao canal in canais.Where(c => c.Atende(mensagem)))
                {
                    await canal.EntregarAsync(mensagem, cancellationToken);
                }

                mensagem.MarcarPublicada(relogio.Agora);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                mensagem.RegistrarFalha(erro.GetType().Name);
                EntregaFalhou(_log, mensagem.Id, mensagem.Tentativas, erro.GetType().Name);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    [LoggerMessage(EventId = 4202, EventName = "outbox.falhou", Level = LogLevel.Warning, Message = "Aviso {mensagemId} não entregue (tentativa {tentativas}): {falha}")]
    private static partial void EntregaFalhou(ILogger logger, Guid mensagemId, int tentativas, string falha);
}
