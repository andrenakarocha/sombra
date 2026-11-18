namespace Sombra.Privacidade.Sinks;

/// <summary>Por onde um dado pessoal tentou sair (os sinks do taint, spec 07 §2).</summary>
public enum SinkDePii
{
    /// <summary>Uma linha de log.</summary>
    Log,

    /// <summary>O corpo de uma resposta HTTP (inclusive de erro), ou um acesso negado a um endpoint com PII.</summary>
    RespostaHttp,

    /// <summary>Uma chamada HTTP para fora (Clima, LLM).</summary>
    SaidaExterna,

    /// <summary>Uma publicação no broker MQTT.</summary>
    Mqtt,

    /// <summary>A mensagem de uma exceção que iria para a resposta.</summary>
    Excecao,
}
