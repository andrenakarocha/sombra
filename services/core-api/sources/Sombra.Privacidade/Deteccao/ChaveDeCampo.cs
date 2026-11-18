using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Compara nomes de campo por palavras, não por substring: <c>cpfDoResponsavel</c> e <c>cpf_titular</c> batem com
/// <c>cpf</c>, mas <c>organizacao</c> não bate com <c>rg</c> e <c>cidade</c> não bate com <c>cid</c>.
/// </summary>
internal static partial class ChaveDeCampo
{
    /// <summary>Quebra um nome de campo em palavras minúsculas e sem acento (camelCase, snake_case, kebab-case).</summary>
    /// <param name="chave">O nome do campo.</param>
    /// <returns>As palavras, na ordem.</returns>
    internal static string[] Palavras(string chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        string semAcento = string.Concat(chave.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark));
        return [.. Palavra().Matches(semAcento).Select(m => m.Value.ToLowerInvariant())];
    }

    /// <summary>Diz se as palavras de uma regra aparecem, em sequência, nas palavras do campo.</summary>
    /// <param name="campo">As palavras do campo.</param>
    /// <param name="regra">As palavras de uma chave da regra.</param>
    /// <returns><c>true</c> quando a sequência aparece inteira.</returns>
    internal static bool Contem(IReadOnlyList<string> campo, IReadOnlyList<string> regra)
    {
        for (int i = 0; i + regra.Count <= campo.Count; i++)
        {
            if (regra.Select((p, j) => campo[i + j] == p).All(igual => igual))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex("[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex Palavra();
}
