using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Autenticacao;

/// <summary>O login e o vínculo dos Cuidadores com as casas.</summary>
public sealed class ContasDeAcesso
{
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ContasDeAcesso"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public ContasDeAcesso(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>Confere e-mail e senha. Gasta o mesmo tempo com ou sem conta, para não revelar quem está cadastrado.</summary>
    /// <param name="email">O e-mail.</param>
    /// <param name="senha">A senha.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A conta, ou nulo quando e-mail ou senha não conferem.</returns>
    public async Task<Usuario?> AutenticarAsync(string email, string senha, CancellationToken cancellationToken)
    {
        string normalizado = Usuario.NormalizarEmail(email);
        Usuario? usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Email == normalizado, cancellationToken);
        if (usuario is null)
        {
            HashDeSenha.Conferir(senha, HashDeSenha.Ficticio);
            return null;
        }

        return usuario.ConferirSenha(senha) ? usuario : null;
    }

    /// <summary>As casas que um Cuidador pode ver.</summary>
    /// <param name="usuarioId">A conta.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>As casas, ou vazio.</returns>
    public async Task<IReadOnlyList<Guid>> CasasDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken)
        => await _db.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId).Select(u => u.Casas).SingleOrDefaultAsync(cancellationToken) ?? [];

    /// <summary>Dá a um Cuidador acesso à casa que ele acabou de cadastrar.</summary>
    /// <param name="usuarioId">A conta.</param>
    /// <param name="casaId">A casa.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    public async Task VincularCasaAsync(Guid usuarioId, Guid casaId, CancellationToken cancellationToken)
    {
        Usuario usuario = await _db.Usuarios.SingleAsync(u => u.Id == usuarioId, cancellationToken);
        usuario.VincularCasa(casaId);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
