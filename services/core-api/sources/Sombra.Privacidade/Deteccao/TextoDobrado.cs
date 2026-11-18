using System;
using System.Globalization;
using System.Text;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Dobra o texto para comparar sem caixa e sem acento, <b>caractere por caractere</b>: o resultado tem o mesmo tamanho
/// da entrada (em NFC), então as posições achadas no texto dobrado valem no original.
/// </summary>
internal static class TextoDobrado
{
    /// <summary>Tira caixa e acento mantendo o tamanho.</summary>
    /// <param name="texto">O texto em NFC.</param>
    /// <returns>O texto dobrado, com o mesmo tamanho.</returns>
    internal static string Dobrar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return string.Create(texto.Length, texto, static (destino, origem) =>
        {
            for (int i = 0; i < origem.Length; i++)
            {
                char c = origem[i];
                destino[i] = c < 128 ? char.ToLowerInvariant(c) : DobrarNaoAscii(c);
            }
        });
    }

    private static char DobrarNaoAscii(char c)
    {
        if (char.IsSurrogate(c))
        {
            return c;
        }

        string decomposto = c.ToString().Normalize(NormalizationForm.FormD);
        char basico = CharUnicodeInfo.GetUnicodeCategory(decomposto[0]) == UnicodeCategory.NonSpacingMark ? c : decomposto[0];
        return char.ToLowerInvariant(basico);
    }
}
