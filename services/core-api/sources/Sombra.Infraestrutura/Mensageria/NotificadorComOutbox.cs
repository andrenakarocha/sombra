using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Mensageria;

/// <summary>
/// O notificador de verdade: põe cada aviso no outbox, dentro da transação do caso de uso (se o caso de uso falha, o
/// aviso não sai). O publicador entrega depois. O mesmo outbox responde quando cada tipo de aviso saiu.
/// </summary>
public sealed class NotificadorComOutbox : INotificador, IHistoricoDeAvisos
{
    private readonly SombraDbContext _db;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="NotificadorComOutbox"/> class.</summary>
    /// <param name="db">O contexto da unidade de trabalho atual.</param>
    /// <param name="relogio">O relógio.</param>
    public NotificadorComOutbox(SombraDbContext db, IRelogio relogio)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <inheritdoc/>
    public Task NotificarAsync(Notificacao notificacao, CancellationToken cancellationToken)
    {
        _db.Outbox.Add(MensagemDeSaida.De(notificacao, _relogio.Agora, Correlacao.Atual));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DateTimeOffset>> MomentosAsync(Guid domicilioId, MotivoDoAviso motivo, DateTimeOffset desde, CancellationToken cancellationToken)
    {
        DateTimeOffset utc = desde.ToUniversalTime();
        return await _db.Outbox.AsNoTracking()
            .Where(m => m.DomicilioId == domicilioId && m.Motivo == motivo && m.CriadaEm >= utc)
            .OrderBy(m => m.CriadaEm)
            .Select(m => m.CriadaEm)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
