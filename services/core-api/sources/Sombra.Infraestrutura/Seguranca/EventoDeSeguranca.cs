using System;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Uma trilha de segurança: o que foi recusado, de onde e quando, só com metadados (nunca o payload nem dado pessoal).
/// A F2.5 acrescenta os eventos da privacidade e o farol de risco.
/// </summary>
public sealed class EventoDeSeguranca
{
    private EventoDeSeguranca(Guid id, DateTimeOffset momento, string tipo, string origem, string? correlationId, string detalhe)
    {
        Id = id;
        Momento = momento;
        Tipo = tipo;
        Origem = origem;
        CorrelationId = correlationId;
        Detalhe = detalhe;
    }

    /// <summary>Gets o identificador.</summary>
    public Guid Id { get; }

    /// <summary>Gets quando aconteceu, em UTC.</summary>
    public DateTimeOffset Momento { get; }

    /// <summary>Gets o tipo, ex.: <c>MQTT_ASSINATURA_INVALIDA</c>.</summary>
    public string Tipo { get; }

    /// <summary>Gets de onde veio (o <c>deviceId</c>, para as mensagens MQTT).</summary>
    public string Origem { get; }

    /// <summary>Gets a correlação.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets o detalhe legível, sem dado pessoal.</summary>
    public string Detalhe { get; }

    /// <summary>Registra um evento.</summary>
    /// <param name="momento">Quando.</param>
    /// <param name="tipo">O tipo.</param>
    /// <param name="origem">De onde.</param>
    /// <param name="correlationId">A correlação.</param>
    /// <param name="detalhe">O detalhe, sem dado pessoal.</param>
    /// <returns>O evento.</returns>
    public static EventoDeSeguranca Registrar(DateTimeOffset momento, string tipo, string origem, string? correlationId, string detalhe)
        => new(Guid.CreateVersion7(), momento.ToUniversalTime(), tipo, origem, correlationId, detalhe);
}
