using System;
using System.Linq;
using System.Security.Claims;
using Sombra.Privacidade.Regras;

namespace Sombra.Api.Privacidade;

/// <summary>
/// Esta família de dado pessoal pode sair em claro neste endpoint, para estes papéis (spec 07 §2.2). Sem o atributo, o
/// <see cref="TaintResponseGuardMiddleware"/> bloqueia a resposta. Ex.: o telefone inteiro para o Cuidador, que liga dali.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermitePiiAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="PermitePiiAttribute"/> class.</summary>
    /// <param name="categoria">A família que pode sair.</param>
    /// <param name="papeis">Os papéis, separados por vírgula; vazio vale para quem chegou ao endpoint.</param>
    public PermitePiiAttribute(CategoriaDePii categoria, string papeis = "")
    {
        Categoria = categoria;
        Papeis = papeis ?? string.Empty;
    }

    /// <summary>Gets a família que pode sair.</summary>
    public CategoriaDePii Categoria { get; }

    /// <summary>Gets os papéis, separados por vírgula.</summary>
    public string Papeis { get; }

    /// <summary>Diz se o usuário pode receber esta família.</summary>
    /// <param name="usuario">Quem pede.</param>
    /// <returns><c>true</c> quando não há papéis ou o usuário tem um deles.</returns>
    public bool Vale(ClaimsPrincipal usuario)
        => string.IsNullOrWhiteSpace(Papeis) || Papeis.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(p => usuario?.IsInRole(p) == true);
}
