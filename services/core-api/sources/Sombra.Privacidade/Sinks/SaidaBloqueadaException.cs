using System;

namespace Sombra.Privacidade.Sinks;

/// <summary>Uma saída externa (HTTP ou MQTT) foi bloqueada porque levava dado pessoal. A mensagem nunca tem o dado.</summary>
public sealed class SaidaBloqueadaException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SaidaBloqueadaException"/> class.</summary>
    /// <param name="sink">Por onde ia sair.</param>
    /// <param name="onde">O host ou o tópico.</param>
    /// <param name="regras">As regras que dispararam.</param>
    public SaidaBloqueadaException(SinkDePii sink, string onde, string regras)
        : base($"Saída {sink} para {onde} bloqueada: levava dado pessoal ({regras}).")
    {
        Sink = sink;
        Onde = onde;
    }

    /// <summary>Initializes a new instance of the <see cref="SaidaBloqueadaException"/> class.</summary>
    public SaidaBloqueadaException()
        : this("Saída bloqueada: levava dado pessoal.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SaidaBloqueadaException"/> class.</summary>
    /// <param name="message">A mensagem.</param>
    public SaidaBloqueadaException(string message)
        : base(message) => Onde = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="SaidaBloqueadaException"/> class.</summary>
    /// <param name="message">A mensagem.</param>
    /// <param name="innerException">A causa.</param>
    public SaidaBloqueadaException(string message, Exception innerException)
        : base(message, innerException) => Onde = string.Empty;

    /// <summary>Gets por onde ia sair.</summary>
    public SinkDePii Sink { get; } = SinkDePii.SaidaExterna;

    /// <summary>Gets o host ou o tópico.</summary>
    public string Onde { get; }
}
