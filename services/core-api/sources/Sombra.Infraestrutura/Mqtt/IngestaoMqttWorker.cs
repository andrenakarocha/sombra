using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sombra.Infraestrutura.Mqtt;

/// <summary>
/// A ingestão MQTT: o MQTTnet entrega as mensagens numa fila (<see cref="Channel{T}"/>) e responde na hora; um consumidor
/// processa uma por vez, cada uma no seu escopo de DI. A conexão se refaz sozinha.
/// </summary>
public sealed partial class IngestaoMqttWorker : BackgroundService
{
    private readonly Channel<MensagemDaSentinela> _fila = Channel.CreateBounded<MensagemDaSentinela>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly ConexaoMqtt _conexao;
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<IngestaoMqttWorker> _log;

    /// <summary>Initializes a new instance of the <see cref="IngestaoMqttWorker"/> class.</summary>
    /// <param name="conexao">A conexão com o broker.</param>
    /// <param name="escopos">A fábrica de escopos.</param>
    /// <param name="log">O log.</param>
    public IngestaoMqttWorker(ConexaoMqtt conexao, IServiceScopeFactory escopos, ILogger<IngestaoMqttWorker> log)
    {
        _conexao = conexao ?? throw new ArgumentNullException(nameof(conexao));
        _escopos = escopos ?? throw new ArgumentNullException(nameof(escopos));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _conexao.MensagemRecebida += Enfileirar;
        return Task.WhenAll(_conexao.ManterConectadaAsync(stoppingToken), ConsumirAsync(stoppingToken));
    }

    private Task Enfileirar(MensagemDaSentinela mensagem)
    {
        _fila.Writer.TryWrite(mensagem);
        return Task.CompletedTask;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Uma mensagem que quebra é registrada; a fila segue com as próximas.")]
    private async Task ConsumirAsync(CancellationToken cancellationToken)
    {
        await foreach (MensagemDaSentinela mensagem in _fila.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<ProcessadorDaSentinela>().ProcessarAsync(mensagem, cancellationToken);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                ProcessamentoFalhou(_log, erro, mensagem.DeviceId, mensagem.Canal);
            }
        }
    }

    [LoggerMessage(EventId = 4306, EventName = "mqtt.processamento_falhou", Level = LogLevel.Error, Message = "A mensagem {canal} da Sentinela {deviceId} quebrou o processamento")]
    private static partial void ProcessamentoFalhou(ILogger logger, Exception excecao, string deviceId, string canal);
}
