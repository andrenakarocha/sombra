using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// As respostas por <c>Idempotency-Key</c> no PostgreSQL. A reserva é um <c>INSERT … ON CONFLICT DO NOTHING</c>, então duas
/// tentativas ao mesmo tempo não executam duas vezes; tudo fora do rastreamento do EF, para não se misturar com a
/// transação do caso de uso.
/// </summary>
public sealed class RegistroDeIdempotencia : IRegistroDeIdempotencia
{
    /// <summary>Por quanto tempo uma chave vale.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    private readonly SombraDbContext _db;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="RegistroDeIdempotencia"/> class.</summary>
    /// <param name="db">O contexto.</param>
    /// <param name="relogio">O relógio.</param>
    public RegistroDeIdempotencia(SombraDbContext db, IRelogio relogio)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <inheritdoc/>
    public async Task<RespostaGuardada?> ObterAsync(Guid usuario, string chave, CancellationToken cancellationToken)
    {
        DateTimeOffset limite = _relogio.Agora - Validade;
        return await _db.RequisicoesIdempotentes.AsNoTracking()
            .Where(r => r.Usuario == usuario && r.Chave == chave && r.CriadaEm >= limite)
            .Select(r => new RespostaGuardada(r.Rota, r.Status, r.Corpo))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ReservarAsync(Guid usuario, string chave, string rota, CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _relogio.Agora;
        DateTimeOffset limite = agora - Validade;
        await _db.RequisicoesIdempotentes.Where(r => r.Usuario == usuario && r.Chave == chave && r.CriadaEm < limite).ExecuteDeleteAsync(cancellationToken);
        int inseridas = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO requisicoes_idempotentes (usuario, chave, rota, criada_em) VALUES ({usuario}, {chave}, {rota}, {agora}) ON CONFLICT DO NOTHING",
            cancellationToken);
        return inseridas == 1;
    }

    /// <inheritdoc/>
    public Task ConcluirAsync(Guid usuario, string chave, int status, string corpo, CancellationToken cancellationToken)
        => _db.RequisicoesIdempotentes
            .Where(r => r.Usuario == usuario && r.Chave == chave)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.Status, status).SetProperty(x => x.Corpo, corpo), cancellationToken);

    /// <inheritdoc/>
    public Task LiberarAsync(Guid usuario, string chave, CancellationToken cancellationToken)
        => _db.RequisicoesIdempotentes.Where(r => r.Usuario == usuario && r.Chave == chave && r.Status == null).ExecuteDeleteAsync(cancellationToken);
}
