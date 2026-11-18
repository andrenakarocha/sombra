using System;
using System.Text.RegularExpressions;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Sentinelas;

/// <summary>A Sentinela (ESP32) de um domicílio, identificada pelo <see cref="DeviceId"/> público do contrato MQTT.</summary>
public sealed partial class Sentinela
{
    /// <summary>De quanto em quanto tempo a Sentinela manda telemetria (contrato MQTT).</summary>
    public static readonly TimeSpan IntervaloDeTelemetria = TimeSpan.FromSeconds(10);

    /// <summary>Quantas telemetrias seguidas podem faltar antes de ela ser considerada offline (regra R9).</summary>
    public const int TelemetriasPerdidasParaOffline = 3;

    private Sentinela(string deviceId) => DeviceId = deviceId;

    /// <summary>Gets o identificador público, <c>snt-</c> seguido de 6 dígitos hexadecimais.</summary>
    public string DeviceId { get; }

    /// <summary>Gets o instante da última mensagem recebida, ou nulo se nunca falou.</summary>
    public DateTimeOffset? UltimoContato { get; private set; }

    /// <summary>Cria uma Sentinela a partir do identificador do contrato.</summary>
    /// <param name="deviceId">O identificador, ex.: <c>snt-7f3a9c</c>.</param>
    /// <returns>A Sentinela.</returns>
    /// <exception cref="RegraVioladaException">Quando o identificador não segue o contrato.</exception>
    public static Sentinela Criar(string? deviceId)
    {
        string id = (deviceId ?? string.Empty).Trim().ToLowerInvariant();
        if (!FormatoDoDeviceId().IsMatch(id))
        {
            throw new RegraVioladaException("R10", "Identificador de Sentinela inválido.");
        }

        return new Sentinela(id);
    }

    /// <summary>Registra que a Sentinela deu sinal de vida.</summary>
    /// <param name="momento">O instante da mensagem.</param>
    public void RegistrarContato(DateTimeOffset momento)
    {
        if (UltimoContato is null || momento > UltimoContato)
        {
            UltimoContato = momento.ToUniversalTime();
        }
    }

    /// <summary>Regra R9: online enquanto não faltarem três telemetrias seguidas.</summary>
    /// <param name="agora">O instante de referência.</param>
    /// <returns><c>true</c> quando o último contato é recente.</returns>
    public bool EstaOnline(DateTimeOffset agora)
        => UltimoContato is { } ultimo && agora - ultimo <= IntervaloDeTelemetria * TelemetriasPerdidasParaOffline;

    [GeneratedRegex("^snt-[0-9a-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoDeviceId();
}
