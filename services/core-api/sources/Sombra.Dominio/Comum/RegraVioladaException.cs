using System;

namespace Sombra.Dominio.Comum;

/// <summary>Uma regra de negócio do domínio foi quebrada. A API devolve 422 com o código da regra.</summary>
public sealed class RegraVioladaException : Exception
{
    /// <summary>Cria a exceção sem regra nem mensagem.</summary>
    public RegraVioladaException()
    {
    }

    /// <summary>Cria a exceção com uma mensagem.</summary>
    /// <param name="message">A mensagem para quem chamou, sem dado pessoal.</param>
    public RegraVioladaException(string message)
        : base(message)
    {
    }

    /// <summary>Cria a exceção com uma mensagem e a causa.</summary>
    /// <param name="message">A mensagem para quem chamou, sem dado pessoal.</param>
    /// <param name="innerException">A exceção que causou esta.</param>
    public RegraVioladaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Cria a exceção com o código da regra (ex.: <c>R1</c>) e a mensagem.</summary>
    /// <param name="regra">O código da regra no plano (docs/specs/01-csharp-core-api.md).</param>
    /// <param name="message">A mensagem para quem chamou, sem dado pessoal.</param>
    public RegraVioladaException(string regra, string message)
        : base(message)
    {
        Regra = regra;
    }

    /// <summary>Gets o código da regra violada, ou vazio quando não se aplica.</summary>
    public string Regra { get; } = string.Empty;
}
