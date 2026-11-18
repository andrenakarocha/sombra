using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Aplicacao.Comum;

namespace Sombra.Infraestrutura.Autenticacao;

/// <summary>
/// Uma conta de acesso ao app e ao painel. Não é do domínio do calor: é de quem opera o Sombra. O banco guarda só o
/// hash da senha.
/// </summary>
public sealed class Usuario
{
    private static readonly string[] _papeis = [Papeis.Cuidador, Papeis.Operador, Papeis.Admin];

    private Usuario(Guid id, string email, string nome, string senhaHash, string papel)
    {
        Id = id;
        Email = email;
        Nome = nome;
        SenhaHash = senhaHash;
        Papel = papel;
    }

    /// <summary>Gets o identificador (o <c>sub</c> do token).</summary>
    public Guid Id { get; }

    /// <summary>Gets o e-mail, em minúsculas.</summary>
    public string Email { get; }

    /// <summary>Gets o nome de exibição.</summary>
    public string Nome { get; }

    /// <summary>Gets o hash PBKDF2 da senha.</summary>
    public string SenhaHash { get; private set; }

    /// <summary>Gets o papel de acesso (<see cref="Papeis"/>).</summary>
    public string Papel { get; }

    /// <summary>Gets as casas que um Cuidador pode ver.</summary>
    public IReadOnlyList<Guid> Casas { get; private set; } = [];

    /// <summary>Cria uma conta.</summary>
    /// <param name="email">O e-mail.</param>
    /// <param name="nome">O nome de exibição.</param>
    /// <param name="senha">A senha, que vira hash na hora.</param>
    /// <param name="papel">O papel de acesso.</param>
    /// <returns>A conta.</returns>
    /// <exception cref="ArgumentException">Com papel desconhecido.</exception>
    public static Usuario Criar(string email, string nome, string senha, string papel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (!_papeis.Contains(papel, StringComparer.Ordinal))
        {
            throw new ArgumentException("Papel de acesso desconhecido.", nameof(papel));
        }

        return new Usuario(Guid.CreateVersion7(), NormalizarEmail(email), nome.Trim(), HashDeSenha.Gerar(senha), papel);
    }

    /// <summary>Deixa o e-mail no formato guardado.</summary>
    /// <param name="email">O e-mail digitado.</param>
    /// <returns>Sem espaços e em minúsculas.</returns>
    public static string NormalizarEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Confere a senha contra o hash, em tempo constante.</summary>
    /// <param name="senha">A senha digitada.</param>
    /// <returns><c>true</c> quando confere.</returns>
    public bool ConferirSenha(string senha) => HashDeSenha.Conferir(senha, SenhaHash);

    /// <summary>Troca a senha.</summary>
    /// <param name="senha">A senha nova.</param>
    public void TrocarSenha(string senha) => SenhaHash = HashDeSenha.Gerar(senha);

    /// <summary>Dá ao Cuidador acesso a uma casa.</summary>
    /// <param name="casaId">A casa.</param>
    public void VincularCasa(Guid casaId)
    {
        if (!Casas.Contains(casaId))
        {
            Casas = [.. Casas, casaId];
        }
    }

    /// <summary>Nunca revela o e-mail.</summary>
    /// <returns>O identificador e o papel.</returns>
    public override string ToString() => $"Usuario {Id} ({Papel})";
}
