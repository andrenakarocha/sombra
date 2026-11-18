using System;
using System.Collections.Generic;
using Sombra.Privacidade.Regras;

namespace Sombra.Api.Privacidade;

/// <summary>
/// Este endpoint trabalha com estas famílias de dado pessoal. Um acesso negado (401/403) a ele vira um vazamento
/// bloqueado: "tentaram acessar o laudo de saúde da Dona Lurdes, mas o Sombra bloqueou" (cenário A3).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DadosPessoaisAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="DadosPessoaisAttribute"/> class.</summary>
    /// <param name="categorias">As famílias que o endpoint guarda.</param>
    public DadosPessoaisAttribute(params CategoriaDePii[] categorias) => Categorias = categorias ?? [];

    /// <summary>Gets as famílias que o endpoint guarda.</summary>
    public IReadOnlyList<CategoriaDePii> Categorias { get; }
}
