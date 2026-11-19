using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;

namespace Sombra.Infraestrutura.Workers;

/// <summary>
/// Esvazia a fila dos guardas: grava cada vazamento bloqueado na trilha de segurança (de onde o farol é calculado e a
/// tradução da IA lê, na F2.6) e registra <c>privacidade.vazamento_bloqueado</c> (log, resposta, exceção, entrada) ou
/// <c>privacidade.saida_externa_bloqueada</c> (HTTP para fora e MQTT), com a correlação de origem. Grava em lote e, depois
/// de cada lote, recalcula o farol global e o empurra para o painel (<c>RiscoCidadaoAtualizado</c>, sem polling).
/// </summary>
public sealed partial class VazamentosWorker : BackgroundService
{
    private const int TamanhoDoLote = 100;

    private readonly FilaDeVazamentos _fila;
    private readonly IServiceScopeFactory _escopos;
    private readonly IRelogio _relogio;
    private readonly ITempoReal _tempoReal;
    private readonly ILogger<VazamentosWorker> _log;

    /// <summary>Initializes a new instance of the <see cref="VazamentosWorker"/> class.</summary>
    /// <param name="fila">A fila dos guardas.</param>
    /// <param name="escopos">Para abrir um contexto do banco por lote.</param>
    /// <param name="relogio">O relógio do sistema.</param>
    /// <param name="tempoReal">O hub, para o farol chegar ao painel na hora.</param>
    /// <param name="log">O log.</param>
    public VazamentosWorker(FilaDeVazamentos fila, IServiceScopeFactory escopos, IRelogio relogio, ITempoReal tempoReal, ILogger<VazamentosWorker> log)
    {
        _fila = fila ?? throw new ArgumentNullException(nameof(fila));
        _escopos = escopos ?? throw new ArgumentNullException(nameof(escopos));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _tempoReal = tempoReal ?? throw new ArgumentNullException(nameof(tempoReal));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _fila.Leitor.WaitToReadAsync(stoppingToken))
        {
            List<VazamentoBloqueado> lote = [];
            while (lote.Count < TamanhoDoLote && _fila.Leitor.TryRead(out VazamentoBloqueado? evento))
            {
                lote.Add(evento);
            }

            try
            {
                await GravarAsync(lote, stoppingToken);
                await PublicarFarolAsync(lote, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                GravacaoFalhou(_log, ex, lote.Count);
            }

            foreach (VazamentoBloqueado evento in lote)
            {
                Registrar(evento);
            }
        }
    }

    private async Task GravarAsync(List<VazamentoBloqueado> lote, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
        SombraDbContext db = escopo.ServiceProvider.GetRequiredService<SombraDbContext>();
        db.EventosDeSeguranca.AddRange(lote.ConvertAll(e => EventoDeSeguranca.DeVazamento(e, _relogio.Agora)));
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PublicarFarolAsync(List<VazamentoBloqueado> lote, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
        RiscoCidadaoResposta risco = await escopo.ServiceProvider.GetRequiredService<IConsultasDoPainel>().RiscoCidadaoAsync(_relogio.Agora, cancellationToken);
        string? correlacao = lote.Select(e => e.CorrelationId).LastOrDefault(c => c is not null);
        string mensagem = risco.Farol switch
        {
            FarolDePrivacidade.Verde => "Nada estranho na última semana.",
            FarolDePrivacidade.Amarelo => "O Sombra barrou tentativas de expor dados. Vale acompanhar.",
            _ => "Muitas tentativas de expor dados sensíveis: o time técnico precisa olhar.",
        };
        await _tempoReal.PublicarAsync(
            EventosDeTempoReal.RiscoCidadaoAtualizado,
            null,
            new RiscoCidadaoAtualizadoEvento(null, risco.Farol, risco.Pontuacao, risco.EventosNaJanela, mensagem, correlacao),
            cancellationToken);
        RiscoCidadaoAtualizado(_log, risco.Farol, risco.Pontuacao, risco.EventosNaJanela);
    }

    private void Registrar(VazamentoBloqueado evento)
    {
        using IDisposable? escopo = _log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = evento.CorrelationId ?? "-" });
        if (!evento.Bloqueado)
        {
            VazamentoPermitido(_log, evento.Sink, evento.Onde, evento.Categoria, evento.RegraId);
        }
        else if (evento.Sink is SinkDePii.SaidaExterna or SinkDePii.Mqtt)
        {
            SaidaExternaBloqueada(_log, evento.Sink, evento.Onde, evento.Categoria, evento.RegraId);
        }
        else
        {
            VazamentoBloqueado(_log, evento.Sink, evento.Onde, evento.Categoria, evento.RegraId, evento.PeloTaint ? "taint" : evento.RegraId == "acesso-negado" ? "controle de acesso" : "conteudo");
        }
    }

    [LoggerMessage(EventId = 4405, EventName = "risco_cidadao.atualizado", Level = LogLevel.Information,
        Message = "Farol de Risco Cidadão {farol} ({pontuacao}, {eventos} evento(s) na janela) enviado ao painel")]
    private static partial void RiscoCidadaoAtualizado(ILogger logger, FarolDePrivacidade farol, double pontuacao, int eventos);

    [LoggerMessage(EventId = 4401, EventName = "privacidade.vazamento_bloqueado", Level = LogLevel.Warning,
        Message = "Vazamento bloqueado no sink {sink} em {onde}: {categoria} (regra {regra}, pelo {deteccao})")]
    private static partial void VazamentoBloqueado(ILogger logger, SinkDePii sink, string onde, CategoriaDePii categoria, string regra, string deteccao);

    [LoggerMessage(EventId = 4402, EventName = "privacidade.saida_externa_bloqueada", Level = LogLevel.Warning,
        Message = "Saída {sink} para {onde} bloqueada: {categoria} (regra {regra})")]
    private static partial void SaidaExternaBloqueada(ILogger logger, SinkDePii sink, string onde, CategoriaDePii categoria, string regra);

    [LoggerMessage(EventId = 4404, EventName = "privacidade.vazamento_permitido", Level = LogLevel.Error,
        Message = "Vazamento NÃO bloqueado (modo inseguro da auditoria) no sink {sink} em {onde}: {categoria} (regra {regra})")]
    private static partial void VazamentoPermitido(ILogger logger, SinkDePii sink, string onde, CategoriaDePii categoria, string regra);

    [LoggerMessage(EventId = 4403, EventName = "privacidade.gravacao_falhou", Level = LogLevel.Error,
        Message = "Não foi possível gravar {quantidade} evento(s) de segurança; eles seguem no log")]
    private static partial void GravacaoFalhou(ILogger logger, Exception excecao, int quantidade);
}
