using System;
using System.Collections.Generic;

namespace Sombra.Privacidade.Regras;

/// <summary>O <c>pii-regras.json</c> não passou na validação do schema. Traz todos os problemas de uma vez.</summary>
public sealed class RegrasDePiiInvalidasException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RegrasDePiiInvalidasException"/> class.</summary>
    /// <param name="erros">Cada problema encontrado, com o caminho dentro do arquivo.</param>
    public RegrasDePiiInvalidasException(IReadOnlyList<string> erros)
        : base($"pii-regras.json inválido ({erros?.Count ?? 0} problemas): {string.Join("; ", erros ?? [])}")
        => Erros = erros ?? [];

    /// <summary>Initializes a new instance of the <see cref="RegrasDePiiInvalidasException"/> class.</summary>
    public RegrasDePiiInvalidasException()
        : this(["pii-regras.json inválido."])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegrasDePiiInvalidasException"/> class.</summary>
    /// <param name="message">A mensagem.</param>
    public RegrasDePiiInvalidasException(string message)
        : this([message])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegrasDePiiInvalidasException"/> class.</summary>
    /// <param name="message">A mensagem.</param>
    /// <param name="innerException">A causa.</param>
    public RegrasDePiiInvalidasException(string message, Exception innerException)
        : base(message, innerException) => Erros = [message];

    /// <summary>Gets os problemas encontrados.</summary>
    public IReadOnlyList<string> Erros { get; } = [];
}
