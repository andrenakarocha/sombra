using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Seguranca;

namespace Sombra.Infraestrutura.Mqtt;

/// <summary>O que aconteceu com uma mensagem da Sentinela.</summary>
public enum ResultadoDaMensagem
{
    /// <summary>Conferida e processada.</summary>
    Aceita = 0,

    /// <summary>JSON ou campos inválidos.</summary>
    Invalida = 1,

    /// <summary>A Sentinela não está vinculada a nenhuma casa.</summary>
    Desconhecida = 2,

    /// <summary>A Sentinela ainda não tem segredo.</summary>
    SemSegredo = 3,

    /// <summary>A assinatura HMAC não confere (R10).</summary>
    AssinaturaInvalida = 4,

    /// <summary>O <c>seq</c> já foi visto (replay, R10).</summary>
    Repetida = 5,

    /// <summary>O <c>ts</c> está fora da janela de 300 s (R10).</summary>
    ForaDaJanela = 6,

    /// <summary>A assinatura conferiu, mas a leitura quebra uma regra (R9).</summary>
    LeituraRecusada = 7,
}

/// <summary>
/// Confere e processa uma mensagem da Sentinela, venha do MQTT ou do POST assinado: assinatura HMAC e anti-replay (R10),
/// depois o caso de uso certo. O que é recusado vira um evento de segurança, só com metadados. A correlação é
/// <c>{deviceId}:{seq}</c> (§3 de <c>docs/observabilidade.md</c>).
/// </summary>
public sealed partial class ProcessadorDaSentinela
{
    private readonly IDomicilioRepository _domicilios;
    private readonly RegistrarLeitura _registrarLeitura;
    private readonly RegistrarCheckIn _registrarCheckIn;
    private readonly SombraDbContext _db;
    private readonly IRelogio _relogio;
    private readonly TimeProvider _tempo;
    private readonly ITempoReal _tempoReal;
    private readonly ILogger<ProcessadorDaSentinela> _log;

    /// <summary>Initializes a new instance of the <see cref="ProcessadorDaSentinela"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="registrarLeitura">O caso de uso da telemetria.</param>
    /// <param name="registrarCheckIn">O caso de uso dos botões.</param>
    /// <param name="db">O contexto (a mesma unidade de trabalho dos casos de uso).</param>
    /// <param name="relogio">O relógio do negócio (pode estar adiantado na demonstração).</param>
    /// <param name="tempo">O tempo real, para a janela anti-replay (o relógio da Sentinela é real).</param>
    /// <param name="tempoReal">O hub.</param>
    /// <param name="log">O log.</param>
    public ProcessadorDaSentinela(IDomicilioRepository domicilios, RegistrarLeitura registrarLeitura, RegistrarCheckIn registrarCheckIn, SombraDbContext db, IRelogio relogio, TimeProvider tempo, ITempoReal tempoReal, ILogger<ProcessadorDaSentinela> log)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _registrarLeitura = registrarLeitura ?? throw new ArgumentNullException(nameof(registrarLeitura));
        _registrarCheckIn = registrarCheckIn ?? throw new ArgumentNullException(nameof(registrarCheckIn));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
        _tempoReal = tempoReal ?? throw new ArgumentNullException(nameof(tempoReal));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Processa uma mensagem.</summary>
    /// <param name="mensagem">A mensagem, como chegou.</param>
    /// <param name="cancellationToken">Cancela o processamento.</param>
    /// <returns>O resultado.</returns>
    public async Task<ResultadoDaMensagem> ProcessarAsync(MensagemDaSentinela mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        if (mensagem.Canal == "status")
        {
            await StatusAsync(mensagem, cancellationToken);
            return ResultadoDaMensagem.Aceita;
        }

        Lida? lida = Ler(mensagem);
        string correlacao = lida is null ? $"{mensagem.DeviceId}:?" : $"{mensagem.DeviceId}:{lida.Seq}";
        Correlacao.Atual = correlacao;
        using IDisposable? escopoDoLog = _log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlacao });
        if (lida is null)
        {
            return await RecusarAsync(mensagem.DeviceId, "MQTT_MENSAGEM_INVALIDA", $"{mensagem.Canal} sem os campos do contrato", ResultadoDaMensagem.Invalida, cancellationToken);
        }

        Domicilio? casa = await _domicilios.ObterPorSentinelaAsync(mensagem.DeviceId, cancellationToken);
        Sentinela? sentinela = casa?.Sentinelas.SingleOrDefault(s => s.DeviceId == mensagem.DeviceId);
        if (casa is null || sentinela is null)
        {
            return await RecusarAsync(mensagem.DeviceId, "MQTT_SENTINELA_DESCONHECIDA", "Sentinela sem casa", ResultadoDaMensagem.Desconhecida, cancellationToken);
        }

        if (sentinela.SegredoHex is null)
        {
            return await RecusarAsync(mensagem.DeviceId, "MQTT_SEM_SEGREDO", "Sentinela sem segredo", ResultadoDaMensagem.SemSegredo, cancellationToken);
        }

        if (!AssinaturaMqtt.Conferir(sentinela.SegredoHex, lida.Canonica(mensagem.DeviceId), lida.Sig))
        {
            AssinaturaInvalida(_log, mensagem.DeviceId, mensagem.Canal, lida.Seq);
            return await RecusarAsync(mensagem.DeviceId, "MQTT_ASSINATURA_INVALIDA", $"{mensagem.Canal} seq {lida.Seq}", ResultadoDaMensagem.AssinaturaInvalida, cancellationToken);
        }

        switch (sentinela.ReceberMensagem(lida.Seq, DateTimeOffset.FromUnixTimeSeconds(lida.Ts), _tempo.GetUtcNow()))
        {
            case RecusaDaMensagem.Repetida:
                ReplayDescartado(_log, mensagem.DeviceId, lida.Seq);
                return await RecusarAsync(mensagem.DeviceId, "MQTT_REPLAY", $"seq {lida.Seq} repetido", ResultadoDaMensagem.Repetida, cancellationToken);
            case RecusaDaMensagem.ForaDaJanela:
                ReplayDescartado(_log, mensagem.DeviceId, lida.Seq);
                return await RecusarAsync(mensagem.DeviceId, "MQTT_FORA_DA_JANELA", $"ts {lida.Ts} fora da janela", ResultadoDaMensagem.ForaDaJanela, cancellationToken);
        }

        return lida.Tipo is null
            ? await TelemetriaAsync(mensagem.DeviceId, lida, cancellationToken)
            : await EventoAsync(casa, lida.Tipo, cancellationToken);
    }

    private static Lida? Ler(MensagemDaSentinela mensagem)
    {
        try
        {
            using JsonDocument documento = JsonDocument.Parse(mensagem.Conteudo);
            JsonElement m = documento.RootElement;
            long v = m.GetProperty("v").GetInt64();
            long ts = m.GetProperty("ts").GetInt64();
            long seq = m.GetProperty("seq").GetInt64();
            string? sig = m.GetProperty("sig").GetString();
            return mensagem.Canal switch
            {
                "telemetria" => new Lida(v, ts, seq, sig, null, m.GetProperty("t").GetDouble(), m.GetProperty("rh").GetDouble(), m.GetProperty("hi").GetDouble(), m.GetProperty("nivel").GetString() ?? string.Empty),
                "evento" => new Lida(v, ts, seq, sig, m.GetProperty("tipo").GetString() ?? string.Empty, 0, 0, 0, string.Empty),
                _ => null,
            };
        }
        catch (Exception erro) when (erro is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private async Task<ResultadoDaMensagem> TelemetriaAsync(string deviceId, Lida lida, CancellationToken cancellationToken)
    {
        try
        {
            await _registrarLeitura.ExecutarAsync(
                new NovaLeituraDto { DeviceId = deviceId, Momento = DateTimeOffset.FromUnixTimeSeconds(lida.Ts), TemperaturaC = lida.T, UmidadePct = lida.Rh },
                cancellationToken);
            return ResultadoDaMensagem.Aceita;
        }
        catch (RegraVioladaException erro)
        {
            LeituraRecusada(_log, deviceId, lida.Seq, erro.Regra);
            await _db.SalvarAsync(cancellationToken);
            return ResultadoDaMensagem.LeituraRecusada;
        }
    }

    private async Task<ResultadoDaMensagem> EventoAsync(Domicilio casa, string tipo, CancellationToken cancellationToken)
    {
        SituacaoDoCheckIn? situacao = tipo switch
        {
            "CHECKIN_ESTOU_BEM" => SituacaoDoCheckIn.EstouBem,
            "PEDIDO_DE_AJUDA" => SituacaoDoCheckIn.PrecisoDeAjuda,
            _ => null,
        };
        if (situacao is null)
        {
            await _db.SalvarAsync(cancellationToken);
            return ResultadoDaMensagem.Aceita;
        }

        await _registrarCheckIn.ExecutarAsync(casa.Id, new NovoCheckInDto { Origem = OrigemDoCheckIn.BotaoSentinela, Situacao = situacao }, cancellationToken);
        return ResultadoDaMensagem.Aceita;
    }

    private async Task StatusAsync(MensagemDaSentinela mensagem, CancellationToken cancellationToken)
    {
        bool online = mensagem.Conteudo.Trim() == "online";
        Domicilio? casa = await _domicilios.ObterPorSentinelaAsync(mensagem.DeviceId, cancellationToken);
        if (casa is null)
        {
            return;
        }

        if (online)
        {
            SentinelaOnline(_log, mensagem.DeviceId, casa.Id);
        }
        else
        {
            SentinelaOffline(_log, mensagem.DeviceId, casa.Id);
        }

        await _tempoReal.PublicarAsync(EventosDeTempoReal.SentinelaStatus, casa.Id, new SentinelaStatusEvento(casa.Id, mensagem.DeviceId, online, Correlacao.Atual), cancellationToken);
    }

    private async Task<ResultadoDaMensagem> RecusarAsync(string origem, string tipo, string detalhe, ResultadoDaMensagem resultado, CancellationToken cancellationToken)
    {
        _db.EventosDeSeguranca.Add(EventoDeSeguranca.Registrar(_relogio.Agora, tipo, origem.Length > 64 ? origem[..64] : origem, Correlacao.Atual, detalhe));
        await _db.SaveChangesAsync(cancellationToken);
        EventoDeSegurancaRegistrado(_log, tipo, origem);
        return resultado;
    }

    [LoggerMessage(EventId = 4301, EventName = "mqtt.assinatura_invalida", Level = LogLevel.Warning, Message = "Assinatura inválida na mensagem {canal} da Sentinela {deviceId} (seq {seq}): descartada")]
    private static partial void AssinaturaInvalida(ILogger logger, string deviceId, string canal, long seq);

    [LoggerMessage(EventId = 4302, EventName = "mqtt.replay_descartado", Level = LogLevel.Warning, Message = "Mensagem repetida ou fora da janela da Sentinela {deviceId} (seq {seq}): descartada")]
    private static partial void ReplayDescartado(ILogger logger, string deviceId, long seq);

    [LoggerMessage(EventId = 4303, EventName = "leitura.recusada", Level = LogLevel.Warning, Message = "Leitura da Sentinela {deviceId} (seq {seq}) recusada pela regra {regra}")]
    private static partial void LeituraRecusada(ILogger logger, string deviceId, long seq, string regra);

    [LoggerMessage(EventId = 4304, EventName = "seguranca.evento_registrado", Level = LogLevel.Warning, Message = "Evento de segurança {tipo} da origem {origem}")]
    private static partial void EventoDeSegurancaRegistrado(ILogger logger, string tipo, string origem);

    [LoggerMessage(EventId = 4305, EventName = "sentinela.online", Level = LogLevel.Information, Message = "Sentinela {deviceId} online na casa {domicilioId}")]
    private static partial void SentinelaOnline(ILogger logger, string deviceId, Guid domicilioId);

    [LoggerMessage(EventId = 4307, EventName = "sentinela.offline", Level = LogLevel.Warning, Message = "Sentinela {deviceId} offline na casa {domicilioId}")]
    private static partial void SentinelaOffline(ILogger logger, string deviceId, Guid domicilioId);
}

/// <summary>Os campos de uma mensagem lida, antes da conferência (telemetria quando não há <c>Tipo</c>).</summary>
/// <param name="V">A versão do contrato.</param>
/// <param name="Ts">Segundos Unix.</param>
/// <param name="Seq">O contador.</param>
/// <param name="Sig">A assinatura recebida.</param>
/// <param name="Tipo">O tipo do evento, ou nulo na telemetria.</param>
/// <param name="T">A temperatura.</param>
/// <param name="Rh">A umidade.</param>
/// <param name="Hi">O índice de calor da Sentinela.</param>
/// <param name="Nivel">O nível da Sentinela.</param>
internal sealed record Lida(long V, long Ts, long Seq, string? Sig, string? Tipo, double T, double Rh, double Hi, string Nivel)
{
    /// <summary>A string canônica que a Sentinela assinou.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <returns>A string canônica.</returns>
    public string Canonica(string deviceId) => Tipo is null
        ? AssinaturaMqtt.CanonicaDaTelemetria(deviceId, V, Ts, Seq, T, Rh, Hi, Nivel)
        : AssinaturaMqtt.CanonicaDoEvento(deviceId, V, Ts, Seq, Tipo);
}
