using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Traducao;

namespace Sombra.Infraestrutura.Ia;

/// <summary>
/// A IA na remediação (spec 07 §3): pega os eventos de segurança ainda sem tradução (o outbox), monta o prompt só com
/// metadados e pede as três traduções. A resposta passa pela <see cref="GuardaDaTraducao"/>; sem chave, com timeout,
/// erro do provedor ou resposta reprovada, cai no template (<c>fonte: template</c>). Eventos com a mesma assinatura
/// reaproveitam a tradução da IA (cache no banco), e cada volta faz no máximo algumas chamadas, para poupar a cota.
/// </summary>
public sealed partial class TradutorDeRisco
{
    private const int TamanhoDoLote = 20;

    private readonly SombraDbContext _db;
    private readonly IModeloDeTraducao _modelo;
    private readonly IRelogio _relogio;
    private readonly ILogger<TradutorDeRisco> _log;
    private readonly TimeSpan _timeout;
    private readonly int _chamadasPorVolta;

    /// <summary>Initializes a new instance of the <see cref="TradutorDeRisco"/> class.</summary>
    /// <param name="db">O banco.</param>
    /// <param name="modelo">O provedor de linguagem.</param>
    /// <param name="relogio">O relógio do sistema.</param>
    /// <param name="configuracao">A configuração: <c>Gemini:TimeoutSegundos</c> (8) e <c>Gemini:ChamadasPorVolta</c> (5).</param>
    /// <param name="log">O log.</param>
    public TradutorDeRisco(SombraDbContext db, IModeloDeTraducao modelo, IRelogio relogio, IConfiguration configuracao, ILogger<TradutorDeRisco> log)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _modelo = modelo ?? throw new ArgumentNullException(nameof(modelo));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _timeout = TimeSpan.FromSeconds(configuracao.GetValue("Gemini:TimeoutSegundos", 8.0));
        _chamadasPorVolta = configuracao.GetValue("Gemini:ChamadasPorVolta", 5);
    }

    /// <summary>Traduz os eventos pendentes.</summary>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>Quantos eventos foram traduzidos (pela IA, pelo cache ou por template).</returns>
    public async Task<int> TraduzirPendentesAsync(CancellationToken cancellationToken)
    {
        List<EventoDeSeguranca> pendentes = await _db.EventosDeSeguranca
            .Where(e => e.TraduzidoEm == null)
            .OrderBy(e => e.Momento)
            .Take(TamanhoDoLote)
            .ToListAsync(cancellationToken);
        Dictionary<string, TraducaoDeRisco> cache = new(StringComparer.Ordinal);
        int chamadas = 0;
        bool provedorDisponivel = _modelo.Configurado;
        int traduzidos = 0;
        foreach (EventoDeSeguranca evento in pendentes)
        {
            DateTimeOffset desde = _relogio.Agora.AddHours(-24);
            EventoParaTraduzir paraTraduzir = evento.ParaTraduzir(1);
            string assinatura = paraTraduzir.Assinatura;
            int repeticoes = await _db.EventosDeSeguranca.CountAsync(e => e.Assinatura == assinatura && e.Momento >= desde, cancellationToken) + 1;

            TraducaoDeRisco? traducao = cache.GetValueOrDefault(assinatura) ?? await DoCacheAsync(assinatura, cancellationToken);
            if (traducao is null && !_modelo.Configurado)
            {
                traducao = Template(paraTraduzir, "sem_chave");
            }
            else if (traducao is null && provedorDisponivel && chamadas < _chamadasPorVolta)
            {
                chamadas++;
                (traducao, bool transitorio) = await PedirAsync(paraTraduzir with { Repeticoes = repeticoes }, cancellationToken);
                provedorDisponivel &= !transitorio;
            }

            if (traducao is null)
            {
                continue; // a cota da volta acabou ou o provedor está instável: fica para a próxima volta
            }

            if (traducao.Fonte == TraducaoDeRisco.FonteIa)
            {
                cache[assinatura] = traducao;
            }

            evento.RegistrarTraducao(assinatura, traducao, _relogio.Agora);
            traduzidos++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return traduzidos;
    }

    private async Task<TraducaoDeRisco?> DoCacheAsync(string assinatura, CancellationToken cancellationToken)
    {
        EventoDeSeguranca? anterior = await _db.EventosDeSeguranca.AsNoTracking()
            .Where(e => e.Assinatura == assinatura && e.FonteDaTraducao == TraducaoDeRisco.FonteIa)
            .OrderByDescending(e => e.TraduzidoEm)
            .FirstOrDefaultAsync(cancellationToken);
        return anterior?.Traducao;
    }

    private async Task<(TraducaoDeRisco Traducao, bool Transitorio)> PedirAsync(EventoParaTraduzir evento, CancellationToken cancellationToken)
    {
        using CancellationTokenSource limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(_timeout);
        long inicio = Stopwatch.GetTimestamp();
        try
        {
            TraducaoDeRisco traducao = await _modelo.TraduzirAsync(PromptDeRisco.Sistema, PromptDeRisco.Montar(evento), limite.Token);
            if (!GuardaDaTraducao.Aprovar(traducao, out string motivo))
            {
                return (Template(evento, "reprovada_" + motivo), false);
            }

            double duracaoMs = Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            string assinatura = evento.Assinatura[..12];
            TraducaoGerada(_log, _modelo.Modelo, duracaoMs, assinatura);
            return (traducao, false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (Template(evento, "timeout"), true);
        }
        catch (HttpRequestException ex)
        {
            return (Template(evento, ex.StatusCode is { } status ? $"http_{(int)status}" : "rede"), true);
        }
        catch (SaidaBloqueadaException)
        {
            return (Template(evento, "prompt_bloqueado"), false);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return (Template(evento, "resposta_invalida"), false);
        }
    }

    private TraducaoDeRisco Template(EventoParaTraduzir evento, string motivo)
    {
        string assinatura = evento.Assinatura[..12];
        FallbackTemplate(_log, motivo, assinatura);
        return TemplatesDeRisco.Para(evento);
    }

    [LoggerMessage(EventId = 4501, EventName = "ia.traducao_gerada", Level = LogLevel.Information,
        Message = "Tradução de risco gerada pelo modelo {modelo} em {duracaoMs} ms (assinatura {assinatura})")]
    private static partial void TraducaoGerada(ILogger logger, string modelo, double duracaoMs, string assinatura);

    [LoggerMessage(EventId = 4502, EventName = "ia.fallback_template", Level = LogLevel.Warning,
        Message = "Tradução de risco por template ({motivo}) para a assinatura {assinatura}")]
    private static partial void FallbackTemplate(ILogger logger, string motivo, string assinatura);
}
