using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Mqtt;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Mensageria;

/// <summary>Um canal de entrega de avisos. Cada aviso do outbox vai a todos os canais que o atendem.</summary>
public interface ICanalDeNotificacao
{
    /// <summary>Diz se este canal entrega este aviso.</summary>
    /// <param name="mensagem">O aviso.</param>
    /// <returns><c>true</c> quando entrega.</returns>
    bool Atende(MensagemDeSaida mensagem);

    /// <summary>Entrega o aviso. Uma exceção faz o publicador tentar de novo depois.</summary>
    /// <param name="mensagem">O aviso.</param>
    /// <param name="cancellationToken">Cancela a entrega.</param>
    /// <returns>A entrega.</returns>
    Task EntregarAsync(MensagemDeSaida mensagem, CancellationToken cancellationToken);
}

/// <summary>
/// A tela da Sentinela: um comando MQTT assinado com o segredo dela e com o <c>seq</c> seguinte (contrato MQTT), no
/// tópico <c>{prefixo}/{deviceId}/comando</c>. O <c>ts</c> e o <c>seq</c> usam o tempo real, porque a Sentinela confere a
/// janela de 300 s com o relógio dela (o relógio da demonstração pode estar adiantado).
/// </summary>
public sealed partial class CanalSentinela : ICanalDeNotificacao
{
    private readonly SombraDbContext _db;
    private readonly ConexaoMqtt _mqtt;
    private readonly TimeProvider _tempo;
    private readonly ILogger<CanalSentinela> _log;

    /// <summary>Initializes a new instance of the <see cref="CanalSentinela"/> class.</summary>
    /// <param name="db">O contexto (o <c>seq</c> do comando é gravado junto com a entrega).</param>
    /// <param name="mqtt">A conexão com o broker.</param>
    /// <param name="tempo">O tempo real.</param>
    /// <param name="log">O log.</param>
    public CanalSentinela(SombraDbContext db, ConexaoMqtt mqtt, TimeProvider tempo, ILogger<CanalSentinela> log)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mqtt = mqtt ?? throw new ArgumentNullException(nameof(mqtt));
        _tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>O tipo do comando do contrato para cada motivo.</summary>
    /// <param name="motivo">O motivo do aviso.</param>
    /// <returns>O tipo do comando.</returns>
    public static string TipoDoComando(MotivoDoAviso motivo) => motivo switch
    {
        MotivoDoAviso.AlertaAberto or MotivoDoAviso.AlertaAgravado => "ALERTA",
        MotivoDoAviso.Escalonamento or MotivoDoAviso.PedidoDeAjuda => "FAMILIA_AVISADA",
        MotivoDoAviso.Previsao => "PRE_ALERTA",
        MotivoDoAviso.LembreteDeAgua => "LEMBRETE_AGUA",
        MotivoDoAviso.CheckInDiario => "CHECKIN_DIARIO",
        _ => "NORMAL",
    };

    /// <inheritdoc/>
    public bool Atende(MensagemDeSaida mensagem) => mensagem?.Destinatario == Destinatario.Morador;

    /// <inheritdoc/>
    public async Task EntregarAsync(MensagemDeSaida mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        Domicilio? casa = await _db.Domicilios.SingleOrDefaultAsync(d => d.Id == mensagem.DomicilioId, cancellationToken);
        Sentinela? sentinela = casa is { Sentinelas.Count: > 0 } ? casa.Sentinelas[0] : null;
        if (sentinela?.SegredoHex is null)
        {
            return;
        }

        DateTimeOffset agora = _tempo.GetUtcNow();
        long ts = agora.ToUnixTimeSeconds();
        long seq = sentinela.ProximoSeqDeComando(agora);
        string tipo = TipoDoComando(mensagem.Motivo);
        string texto = mensagem.Mensagem.Length > 80 ? mensagem.Mensagem[..80] : mensagem.Mensagem;
        string sig = AssinaturaMqtt.Assinar(sentinela.SegredoHex, AssinaturaMqtt.CanonicaDoComando(sentinela.DeviceId, 1, ts, seq, tipo, texto));
        string conteudo = JsonSerializer.Serialize(new { v = 1, ts, seq, tipo, mensagem = texto, sig });
        await _mqtt.PublicarAsync($"{_mqtt.Prefixo}/{sentinela.DeviceId}/comando", conteudo, cancellationToken);
        ComandoPublicado(_log, sentinela.DeviceId, seq, tipo);
    }

    [LoggerMessage(EventId = 4101, EventName = "comando.publicado", Level = LogLevel.Information, Message = "Comando {tipo} para a Sentinela {deviceId} (seq {seq})")]
    private static partial void ComandoPublicado(ILogger logger, string deviceId, long seq, string tipo);
}

/// <summary>O app e o painel, pelo hub SignalR: o escalonamento vira <c>AlertaEscalonado</c>; os outros avisos, <c>AvisoParaARede</c>.</summary>
public sealed class CanalApp : ICanalDeNotificacao
{
    private readonly ITempoReal _tempoReal;

    /// <summary>Initializes a new instance of the <see cref="CanalApp"/> class.</summary>
    /// <param name="tempoReal">O hub.</param>
    public CanalApp(ITempoReal tempoReal) => _tempoReal = tempoReal ?? throw new ArgumentNullException(nameof(tempoReal));

    /// <inheritdoc/>
    public bool Atende(MensagemDeSaida mensagem) => mensagem is not null && mensagem.Destinatario != Destinatario.Morador;

    /// <inheritdoc/>
    public Task EntregarAsync(MensagemDeSaida mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        return mensagem is { Motivo: MotivoDoAviso.Escalonamento or MotivoDoAviso.PedidoDeAjuda, AlertaId: { } alertaId }
            ? _tempoReal.PublicarAsync(EventosDeTempoReal.AlertaEscalonado, mensagem.DomicilioId, new AlertaEscalonadoEvento(
                alertaId, mensagem.DomicilioId, (int)mensagem.Destinatario, mensagem.Destinatario, mensagem.Mensagem, mensagem.CorrelationId), cancellationToken)
            : _tempoReal.PublicarAsync(EventosDeTempoReal.AvisoParaARede, mensagem.DomicilioId, new AvisoParaARedeEvento(
                mensagem.DomicilioId, mensagem.AlertaId, mensagem.Destinatario, mensagem.Motivo, mensagem.Mensagem, mensagem.CorrelationId), cancellationToken);
    }
}

/// <summary>O SMS do protótipo: só registra no log quantos contatos receberiam, nunca o número nem o nome.</summary>
public sealed partial class CanalSmsSimulado : ICanalDeNotificacao
{
    private readonly ILogger<CanalSmsSimulado> _log;

    /// <summary>Initializes a new instance of the <see cref="CanalSmsSimulado"/> class.</summary>
    /// <param name="log">O log.</param>
    public CanalSmsSimulado(ILogger<CanalSmsSimulado> log) => _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <inheritdoc/>
    public bool Atende(MensagemDeSaida mensagem) => mensagem is not null && mensagem.Destinatario != Destinatario.Morador && mensagem.Contatos > 0;

    /// <inheritdoc/>
    public Task EntregarAsync(MensagemDeSaida mensagem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        SmsSimulado(_log, mensagem.Contatos, mensagem.Destinatario, mensagem.DomicilioId);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 4102, EventName = "sms.simulado", Level = LogLevel.Information, Message = "SMS simulado para {contatos} contato(s) ({destinatario}) da casa {domicilioId}")]
    private static partial void SmsSimulado(ILogger logger, int contatos, Destinatario destinatario, Guid domicilioId);
}
