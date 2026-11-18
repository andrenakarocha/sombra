using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;

namespace Sombra.Infraestrutura.Notificacoes;

/// <summary>
/// Registra os avisos no log até a F1.9 trazer o outbox e os canais (app, Sentinela, SMS simulado). O log leva só o
/// destinatário, a quantidade de contatos e os IDs: nunca nome, telefone ou a mensagem.
/// </summary>
public sealed partial class NotificadorDeLog : INotificador
{
    private readonly ILogger<NotificadorDeLog> _log;

    /// <summary>Initializes a new instance of the <see cref="NotificadorDeLog"/> class.</summary>
    /// <param name="log">O log.</param>
    public NotificadorDeLog(ILogger<NotificadorDeLog> log) => _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <inheritdoc/>
    public Task NotificarAsync(Notificacao notificacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificacao);
        AvisoRegistrado(_log, notificacao.Destinatario, notificacao.Contatos.Count, notificacao.AlertaId, notificacao.DomicilioId);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Aviso para {Destinatario} ({Contatos} contatos) do alerta {AlertaId} da casa {DomicilioId}")]
    private static partial void AvisoRegistrado(ILogger logger, Destinatario destinatario, int contatos, Guid alertaId, Guid domicilioId);
}
