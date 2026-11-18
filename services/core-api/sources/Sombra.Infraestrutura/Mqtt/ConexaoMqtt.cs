using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Sombra.Aplicacao.Comum;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Infraestrutura.Mqtt;

/// <summary>Uma mensagem que chegou da Sentinela, antes de qualquer conferência.</summary>
/// <param name="DeviceId">A Sentinela, tirada do tópico.</param>
/// <param name="Canal"><c>telemetria</c>, <c>evento</c> ou <c>status</c>.</param>
/// <param name="Conteudo">O payload, como texto.</param>
public sealed record MensagemDaSentinela(string DeviceId, string Canal, string Conteudo);

/// <summary>
/// A conexão do Core com o broker (Mosquitto no compose). Assina <c>{prefixo}/+/telemetria</c>, <c>/evento</c> e
/// <c>/status</c>, publica os comandos e se reconecta sozinha quando cai.
/// </summary>
public sealed partial class ConexaoMqtt : IDisposable
{
    private static readonly string[] _canais = ["telemetria", "evento", "status"];
    private readonly IMqttClient _cliente;
    private readonly MqttClientOptions _opcoes;
    private readonly ILogger<ConexaoMqtt> _log;
    private readonly GuardaDeSaida _guarda;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ModoDaPrivacidade _modo;

    /// <summary>Initializes a new instance of the <see cref="ConexaoMqtt"/> class.</summary>
    /// <param name="configuracao">A configuração (<c>Mqtt:Host</c>, <c>Mqtt:Porta</c>, <c>Mqtt:PrefixoTopico</c>).</param>
    /// <param name="log">O log.</param>
    /// <param name="guarda">A guarda de saída: nada com dado pessoal é publicado.</param>
    /// <param name="observador">Quem registra uma publicação bloqueada.</param>
    /// <param name="modo">Se bloqueia ou só registra (modo inseguro da auditoria).</param>
    public ConexaoMqtt(IConfiguration configuracao, ILogger<ConexaoMqtt> log, GuardaDeSaida guarda, IObservadorDeVazamentos observador, ModoDaPrivacidade modo)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _guarda = guarda ?? throw new ArgumentNullException(nameof(guarda));
        _observador = observador ?? throw new ArgumentNullException(nameof(observador));
        _modo = modo ?? throw new ArgumentNullException(nameof(modo));
        Host = configuracao["Mqtt:Host"] ?? "localhost";
        Porta = configuracao.GetValue("Mqtt:Porta", 1883);
        Prefixo = configuracao["Mqtt:PrefixoTopico"] ?? "sombra/v1";
        _opcoes = new MqttClientOptionsBuilder()
            .WithTcpServer(Host, Porta)
            .WithClientId("sombra-core-" + Guid.NewGuid().ToString("N")[..10])
            .WithCleanSession(true)
            .Build();
        _cliente = new MqttClientFactory().CreateMqttClient();
        _cliente.ApplicationMessageReceivedAsync += argumentos =>
        {
            string[] partes = argumentos.ApplicationMessage.Topic.Split('/');
            return partes.Length >= 2 && MensagemRecebida is { } aviso
                ? aviso(new MensagemDaSentinela(partes[^2], partes[^1], argumentos.ApplicationMessage.ConvertPayloadToString()))
                : Task.CompletedTask;
        };
    }

    /// <summary>Avisa a cada mensagem recebida (a ingestão põe numa fila e responde na hora).</summary>
    public event Func<MensagemDaSentinela, Task>? MensagemRecebida;

    /// <summary>Gets o host do broker.</summary>
    public string Host { get; }

    /// <summary>Gets a porta do broker.</summary>
    public int Porta { get; }

    /// <summary>Gets o prefixo dos tópicos (<c>sombra/v1</c>).</summary>
    public string Prefixo { get; }

    /// <summary>Gets a value indicating whether está conectada agora.</summary>
    public bool EstaConectada => _cliente.IsConnected;

    /// <summary>Mantém a conexão até o Core parar: conecta, assina os tópicos e reconecta a cada 5 s quando cai.</summary>
    /// <param name="cancellationToken">Para quando o Core para.</param>
    /// <returns>A manutenção.</returns>
    public async Task ManterConectadaAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_cliente.IsConnected)
            {
                try
                {
                    await _cliente.ConnectAsync(_opcoes, cancellationToken);
                    MqttClientSubscribeOptionsBuilder assinatura = new();
                    foreach (string canal in _canais)
                    {
                        assinatura.WithTopicFilter($"{Prefixo}/+/{canal}", MqttQualityOfServiceLevel.AtLeastOnce);
                    }

                    await _cliente.SubscribeAsync(assinatura.Build(), cancellationToken);
                    Conectada(_log, Host, Porta);
                }
                catch (Exception erro) when (erro is MQTTnet.Exceptions.MqttCommunicationException or OperationCanceledException or TimeoutException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    SemConexao(_log, Host, Porta, erro.GetType().Name);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <summary>Publica uma mensagem (QoS 1).</summary>
    /// <param name="topico">O tópico.</param>
    /// <param name="conteudo">O JSON.</param>
    /// <param name="cancellationToken">Cancela a publicação.</param>
    /// <returns>A publicação.</returns>
    /// <exception cref="InvalidOperationException">Sem conexão com o broker (o outbox tenta de novo).</exception>
    /// <exception cref="SaidaBloqueadaException">O conteúdo levava dado pessoal: nada foi publicado (spec 07 §2.3).</exception>
    public async Task PublicarAsync(string topico, string conteudo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topico);
        ResultadoDaGuarda guarda = _guarda.Verificar(conteudo, ContextoDeTaint.Atual);
        if (!guarda.Limpo)
        {
            foreach (AchadoNaSaida a in guarda.Achados)
            {
                _observador.Registrar(new VazamentoBloqueado(SinkDePii.Mqtt, topico, a.RegraId, a.Categoria, a.Severidade, a.PeloTaint, Correlacao.Atual, Bloqueado: _modo.Bloqueia));
            }

            if (_modo.Bloqueia)
            {
                throw new SaidaBloqueadaException(SinkDePii.Mqtt, topico, string.Join(", ", guarda.Achados.Select(a => a.RegraId).Distinct()));
            }
        }

        if (!_cliente.IsConnected)
        {
            throw new InvalidOperationException("Sem conexão com o broker MQTT.");
        }

        await _cliente.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic(topico)
                .WithPayload(Encoding.UTF8.GetBytes(conteudo))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() => _cliente.Dispose();

    [LoggerMessage(EventId = 4001, EventName = "mqtt.conectado", Level = LogLevel.Information, Message = "Conectado ao broker {host}:{porta}")]
    private static partial void Conectada(ILogger logger, string host, int porta);

    [LoggerMessage(EventId = 4002, EventName = "mqtt.desconectado", Level = LogLevel.Warning, Message = "Sem conexão com o broker {host}:{porta} ({motivo}); nova tentativa em 5 s")]
    private static partial void SemConexao(ILogger logger, string host, int porta, string motivo);
}
