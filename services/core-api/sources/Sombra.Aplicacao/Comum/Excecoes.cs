using System;
using System.Collections.Generic;

namespace Sombra.Aplicacao.Comum;

/// <summary>O DTO de entrada não passou na validação. A API devolve 400 com os erros por campo.</summary>
public sealed class ValidacaoException : Exception
{
    /// <summary>Cria a exceção sem erros por campo.</summary>
    public ValidacaoException()
        : base("Dados inválidos.")
    {
    }

    /// <summary>Cria a exceção com uma mensagem.</summary>
    /// <param name="message">A mensagem, sem dado pessoal.</param>
    public ValidacaoException(string message)
        : base(message)
    {
    }

    /// <summary>Cria a exceção com uma mensagem e a causa.</summary>
    /// <param name="message">A mensagem, sem dado pessoal.</param>
    /// <param name="innerException">A exceção que causou esta.</param>
    public ValidacaoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Cria a exceção com os erros por campo.</summary>
    /// <param name="erros">As mensagens de cada campo (nome em camelCase, como no JSON).</param>
    public ValidacaoException(IReadOnlyDictionary<string, string[]> erros)
        : base("Dados inválidos.")
    {
        Erros = erros;
    }

    /// <summary>Gets as mensagens de cada campo.</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; } = new Dictionary<string, string[]>();
}

/// <summary>O recurso pedido não existe. A API devolve 404.</summary>
public sealed class RecursoNaoEncontradoException : Exception
{
    /// <summary>Cria a exceção com a mensagem padrão.</summary>
    public RecursoNaoEncontradoException()
        : base("Recurso não encontrado.")
    {
    }

    /// <summary>Cria a exceção com uma mensagem.</summary>
    /// <param name="message">A mensagem, sem dado pessoal (ex.: "Domicílio não encontrado.").</param>
    public RecursoNaoEncontradoException(string message)
        : base(message)
    {
    }

    /// <summary>Cria a exceção com uma mensagem e a causa.</summary>
    /// <param name="message">A mensagem, sem dado pessoal.</param>
    /// <param name="innerException">A exceção que causou esta.</param>
    public RecursoNaoEncontradoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>O pedido conflita com o estado atual (ex.: Sentinela já vinculada a outra casa). A API devolve 409.</summary>
public sealed class ConflitoException : Exception
{
    /// <summary>Cria a exceção com a mensagem padrão.</summary>
    public ConflitoException()
        : base("O pedido conflita com o estado atual.")
    {
    }

    /// <summary>Cria a exceção com uma mensagem.</summary>
    /// <param name="message">A mensagem, sem dado pessoal.</param>
    public ConflitoException(string message)
        : base(message)
    {
    }

    /// <summary>Cria a exceção com uma mensagem e a causa.</summary>
    /// <param name="message">A mensagem, sem dado pessoal.</param>
    /// <param name="innerException">A exceção que causou esta.</param>
    public ConflitoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
