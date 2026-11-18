using System;

namespace Sombra.Privacidade;

/// <summary>
/// As máscaras de PII das respostas e das telas, nos formatos da spec 07. Recebe os dígitos já validados pelos value
/// objects do domínio. A F2.2 acrescenta as máscaras das demais regras do <c>pii-regras.json</c>.
/// </summary>
public static class MascaradorDePii
{
    /// <summary>O texto que substitui qualquer dado de saúde.</summary>
    public const string SaudeProtegida = "[DADO DE SAÚDE PROTEGIDO]";

    /// <summary>Mascara um CPF mantendo o bloco do meio e os verificadores: <c>***.456.***-00</c>.</summary>
    /// <param name="digitos">Os 11 dígitos.</param>
    /// <returns>O CPF mascarado.</returns>
    public static string MascararCpf(string digitos)
    {
        ArgumentNullException.ThrowIfNull(digitos);
        return $"***.{digitos[3..6]}.***-{digitos[9..]}";
    }

    /// <summary>Mascara um Cartão SUS mantendo os quatro últimos dígitos: <c>*** **** **** 1234</c>.</summary>
    /// <param name="digitos">Os 15 dígitos.</param>
    /// <returns>O Cartão SUS mascarado.</returns>
    public static string MascararCartaoSus(string digitos)
    {
        ArgumentNullException.ThrowIfNull(digitos);
        return $"*** **** **** {digitos[^4..]}";
    }

    /// <summary>Mascara um telefone mantendo o DDD e os quatro últimos dígitos: <c>(11) *****-4321</c>.</summary>
    /// <param name="digitos">O DDD e o número, só dígitos.</param>
    /// <returns>O telefone mascarado.</returns>
    public static string MascararTelefone(string digitos)
    {
        ArgumentNullException.ThrowIfNull(digitos);
        return $"({digitos[..2]}) *****-{digitos[^4..]}";
    }
}
