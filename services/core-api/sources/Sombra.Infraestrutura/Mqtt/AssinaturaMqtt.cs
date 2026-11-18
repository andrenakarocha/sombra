using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Sombra.Infraestrutura.Mqtt;

/// <summary>
/// A assinatura das mensagens MQTT (contrato <c>docs/contratos/mqtt.asyncapi.yaml</c>): HMAC-SHA256, em hexadecimal
/// minúsculo, da string canônica, com o segredo de 32 bytes da Sentinela. Os números vão com uma casa e ponto.
/// </summary>
public static class AssinaturaMqtt
{
    /// <summary>A string canônica de uma telemetria: <c>T|v|deviceId|ts|seq|t|rh|hi|nivel</c>.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="v">A versão do contrato.</param>
    /// <param name="ts">Segundos Unix.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="t">A temperatura.</param>
    /// <param name="rh">A umidade.</param>
    /// <param name="hi">O índice de calor da Sentinela.</param>
    /// <param name="nivel">O nível calculado na Sentinela.</param>
    /// <returns>A string canônica.</returns>
    public static string CanonicaDaTelemetria(string deviceId, long v, long ts, long seq, double t, double rh, double hi, string nivel)
        => string.Join('|', "T", Inteiro(v), deviceId, Inteiro(ts), Inteiro(seq), Decimal(t), Decimal(rh), Decimal(hi), nivel);

    /// <summary>A string canônica de um evento: <c>E|v|deviceId|ts|seq|tipo</c>.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="v">A versão do contrato.</param>
    /// <param name="ts">Segundos Unix.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="tipo">O tipo do evento.</param>
    /// <returns>A string canônica.</returns>
    public static string CanonicaDoEvento(string deviceId, long v, long ts, long seq, string tipo)
        => string.Join('|', "E", Inteiro(v), deviceId, Inteiro(ts), Inteiro(seq), tipo);

    /// <summary>A string canônica de um comando: <c>C|v|deviceId|ts|seq|tipo|mensagem</c>.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="v">A versão do contrato.</param>
    /// <param name="ts">Segundos Unix.</param>
    /// <param name="seq">O contador.</param>
    /// <param name="tipo">O tipo do comando.</param>
    /// <param name="mensagem">O texto para a tela.</param>
    /// <returns>A string canônica.</returns>
    public static string CanonicaDoComando(string deviceId, long v, long ts, long seq, string tipo, string mensagem)
        => string.Join('|', "C", Inteiro(v), deviceId, Inteiro(ts), Inteiro(seq), tipo, mensagem);

    /// <summary>Assina uma string canônica.</summary>
    /// <param name="segredoHex">O segredo, 32 bytes em hexadecimal.</param>
    /// <param name="canonica">A string canônica.</param>
    /// <returns>A assinatura em hexadecimal minúsculo.</returns>
    public static string Assinar(string segredoHex, string canonica)
    {
        ArgumentException.ThrowIfNullOrEmpty(segredoHex);
        ArgumentNullException.ThrowIfNull(canonica);
        return Convert.ToHexStringLower(HMACSHA256.HashData(Convert.FromHexString(segredoHex), Encoding.UTF8.GetBytes(canonica)));
    }

    /// <summary>Confere uma assinatura, em tempo constante.</summary>
    /// <param name="segredoHex">O segredo.</param>
    /// <param name="canonica">A string canônica.</param>
    /// <param name="assinatura">A assinatura recebida.</param>
    /// <returns><c>true</c> quando confere.</returns>
    public static bool Conferir(string segredoHex, string canonica, string? assinatura)
    {
        string esperada = Assinar(segredoHex, canonica);
        return assinatura is not null
            && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperada), Encoding.ASCII.GetBytes(assinatura.ToLowerInvariant()));
    }

    private static string Inteiro(long valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static string Decimal(double valor) => valor.ToString("F1", CultureInfo.InvariantCulture);
}
