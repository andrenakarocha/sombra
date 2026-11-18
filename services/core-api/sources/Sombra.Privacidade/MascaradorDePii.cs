using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sombra.Privacidade;

/// <summary>
/// As máscaras de PII das respostas, das telas e do texto livre, nos formatos da spec 07. Cada regra do
/// <c>pii-regras.json</c> escolhe um <see cref="Formatos">formato</see> pelo campo <c>mascara</c>.
/// </summary>
public static class MascaradorDePii
{
    /// <summary>O texto que substitui qualquer dado de saúde.</summary>
    public const string SaudeProtegida = "[DADO DE SAÚDE PROTEGIDO]";

    /// <summary>O texto que substitui senhas, tokens e chaves.</summary>
    public const string Removido = "[REMOVIDO]";

    /// <summary>Gets os formatos que uma regra pode usar no campo <c>mascara</c>.</summary>
    public static IReadOnlyList<string> Formatos { get; } = ["cpf", "cns", "telefone", "email", "saude", "removido", "parcial"];

    /// <summary>Mascara um trecho no formato pedido. Se o trecho não tem a forma esperada, cai no formato parcial.</summary>
    /// <param name="formato">Um dos <see cref="Formatos"/>.</param>
    /// <param name="trecho">O dado achado.</param>
    /// <returns>O trecho mascarado.</returns>
    /// <exception cref="ArgumentException">Quando o formato não existe.</exception>
    public static string Aplicar(string formato, string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);
        string digitos = new([.. trecho.Where(char.IsAsciiDigit)]);
        return formato switch
        {
            "cpf" when digitos.Length == 11 => MascararCpf(digitos),
            "cns" when digitos.Length == 15 => MascararCartaoSus(digitos),
            "telefone" when digitos.Length is >= 10 and <= 13 => MascararTelefone(digitos.Length > 11 ? digitos[2..] : digitos),
            "email" when trecho.Contains('@', StringComparison.Ordinal) => MascararEmail(trecho),
            "saude" => SaudeProtegida,
            "removido" => Removido,
            "cpf" or "cns" or "telefone" or "email" or "parcial" => MascararParcial(trecho),
            _ => throw new ArgumentException($"formato de máscara '{formato}' desconhecido", nameof(formato)),
        };
    }

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

    /// <summary>Mascara um e-mail mantendo a primeira letra do usuário e do domínio: <c>l***@d***.com</c>.</summary>
    /// <param name="email">O e-mail.</param>
    /// <returns>O e-mail mascarado.</returns>
    public static string MascararEmail(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        int arroba = email.IndexOf('@', StringComparison.Ordinal);
        string dominio = email[(arroba + 1)..];
        int ponto = dominio.IndexOf('.', StringComparison.Ordinal);
        string sufixo = ponto < 0 ? string.Empty : dominio[ponto..];
        return $"{email[..Math.Min(1, arroba)]}***@{dominio[..Math.Min(1, dominio.Length)]}***{sufixo}";
    }

    /// <summary>
    /// Troca letras e dígitos por <c>*</c>, menos os dois últimos (todos, se o trecho for curto), e mantém a pontuação:
    /// <c>**.***.**8-9</c>.
    /// </summary>
    /// <param name="trecho">O trecho.</param>
    /// <returns>O trecho mascarado.</returns>
    public static string MascararParcial(string trecho)
    {
        ArgumentNullException.ThrowIfNull(trecho);
        int visiveis = trecho.Count(char.IsLetterOrDigit) > 4 ? 2 : 0;
        StringBuilder resultado = new(trecho);
        for (int i = trecho.Length - 1; i >= 0; i--)
        {
            if (char.IsLetterOrDigit(trecho[i]))
            {
                resultado[i] = visiveis-- > 0 ? trecho[i] : '*';
            }
        }

        return resultado.ToString();
    }
}
