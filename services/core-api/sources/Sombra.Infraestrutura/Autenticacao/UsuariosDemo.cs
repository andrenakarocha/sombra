using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Comum;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Autenticacao;

/// <summary>
/// As contas da demonstração, ligadas às casas fictícias do seed. A senha vem do ambiente (<c>Demo__Senha</c>, gerada
/// pelo <c>make env</c>) e nunca fica no repositório; sem ela, nenhuma conta é criada.
/// </summary>
public static class UsuariosDemo
{
    /// <summary>A casa da Dona Lurdes no seed.</summary>
    public static readonly Guid CasaDaLurdes = new("0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10");

    /// <summary>A casa do Seu Joaquim no seed.</summary>
    public static readonly Guid CasaDoJoaquim = new("4e2b7c90-58a1-4d3f-9b6e-1a7c2d9e8f31");

    private static readonly (string Email, string Nome, string Papel, Guid[] Casas)[] _contas =
    [
        ("ana.filha@demo.sombra", "Ana (filha da Dona Lurdes)", Papeis.Cuidador, [CasaDaLurdes]),
        ("marcos.filho@demo.sombra", "Marcos (filho do Seu Joaquim)", Papeis.Cuidador, [CasaDoJoaquim]),
        ("operador@demo.sombra", "Operação do Sombra", Papeis.Operador, []),
        ("admin@demo.sombra", "Administração do Sombra", Papeis.Admin, []),
    ];

    /// <summary>Cria as contas que faltam e acerta a senha das que já existem.</summary>
    /// <param name="db">O contexto.</param>
    /// <param name="senha">A senha da demonstração.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    public static async Task SemearAsync(SombraDbContext db, string senha, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        foreach ((string email, string nome, string papel, Guid[] casas) in _contas)
        {
            Usuario? usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
            if (usuario is null)
            {
                usuario = Usuario.Criar(email, nome, senha, papel);
                db.Usuarios.Add(usuario);
            }
            else if (!usuario.ConferirSenha(senha))
            {
                usuario.TrocarSenha(senha);
            }

            foreach (Guid casa in casas)
            {
                usuario.VincularCasa(casa);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
