using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Os validadores que confirmam um candidato achado pelo padrão da regra. O <c>pii-regras.json</c> escolhe um pelo
/// nome. É o que impede o motor de marcar qualquer número de 11 dígitos como CPF.
/// </summary>
/// <remarks>
/// CPF e Cartão SUS repetem o algoritmo dos value objects do domínio de propósito: o módulo de privacidade não depende
/// de nenhuma camada (teste de arquitetura <c>PrivacidadeEhIndependente</c>).
/// </remarks>
public static partial class Validadores
{
    /// <summary>O validador que aceita qualquer candidato.</summary>
    public const string Nenhum = "nenhum";

    private const int TamanhoMinimoDeSegredo = 24;
    private const double EntropiaMinimaDeSegredo = 3.5;

    // DDDs em uso no Brasil (Anatel, Plano de Numeração).
    private static readonly FrozenSet<int> _ddds = new[]
    {
        11, 12, 13, 14, 15, 16, 17, 18, 19, 21, 22, 24, 27, 28, 31, 32, 33, 34, 35, 37, 38,
        41, 42, 43, 44, 45, 46, 47, 48, 49, 51, 53, 54, 55, 61, 62, 63, 64, 65, 66, 67, 68, 69,
        71, 73, 74, 75, 77, 79, 81, 82, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 96, 97, 98, 99,
    }.ToFrozenSet();

    private static readonly FrozenDictionary<string, Func<string, bool>> _porNome = new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
    {
        [Nenhum] = static _ => true,
        ["cpf"] = Cpf,
        ["cns"] = CartaoSus,
        ["telefone"] = Telefone,
        ["credencial"] = Credencial,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Gets os nomes que o <c>pii-regras.json</c> pode usar no campo <c>validador</c>.</summary>
    public static IReadOnlyCollection<string> Nomes => _porNome.Keys;

    /// <summary>Busca um validador pelo nome.</summary>
    /// <param name="nome">O nome do validador.</param>
    /// <param name="validador">O validador, quando existe.</param>
    /// <returns><c>true</c> quando o nome é conhecido.</returns>
    public static bool TentarObter(string nome, out Func<string, bool> validador)
        => _porNome.TryGetValue(nome, out validador!);

    /// <summary>CPF: 11 dígitos, não todos iguais, com os dois verificadores certos.</summary>
    /// <param name="candidato">O texto achado, com ou sem pontuação.</param>
    /// <returns><c>true</c> quando é um CPF válido.</returns>
    public static bool Cpf(string candidato)
    {
        int[] d = Digitos(candidato);
        if (d.Length != 11 || d.All(x => x == d[0]))
        {
            return false;
        }

        return d[9] == VerificadorDoCpf(d, 9) && d[10] == VerificadorDoCpf(d, 10);
    }

    /// <summary>Cartão SUS: 15 dígitos, começa em 1, 2, 7, 8 ou 9 e a soma ponderada (15 a 1) é múltipla de 11.</summary>
    /// <param name="candidato">O texto achado, com ou sem espaços.</param>
    /// <returns><c>true</c> quando passa na validação oficial.</returns>
    public static bool CartaoSus(string candidato)
    {
        int[] d = Digitos(candidato);
        if (d.Length != 15 || d[0] is not (1 or 2 or 7 or 8 or 9))
        {
            return false;
        }

        return d.Select((x, i) => x * (15 - i)).Sum() % 11 == 0;
    }

    /// <summary>Telefone brasileiro: DDD em uso; celular com 9 dígitos começando em 9, fixo com 8 começando em 2 a 5.</summary>
    /// <param name="candidato">O texto achado, com ou sem +55, parênteses e hífen.</param>
    /// <returns><c>true</c> quando é um número possível.</returns>
    public static bool Telefone(string candidato)
    {
        int[] d = Digitos(candidato);
        if (d.Length is 12 or 13 && d[0] == 5 && d[1] == 5)
        {
            d = d[2..];
        }

        if (d.Length is not (10 or 11) || !_ddds.Contains((d[0] * 10) + d[1]))
        {
            return false;
        }

        return d.Length == 11 ? d[2] == 9 : d[2] is >= 2 and <= 5;
    }

    /// <summary>
    /// Credencial: JWT e <c>Bearer</c> valem sempre; um token solto só vale com 24 caracteres ou mais, letras e
    /// dígitos misturados e entropia alta. UUIDs (como o <c>correlationId</c>) não são segredo.
    /// </summary>
    /// <param name="candidato">O texto achado.</param>
    /// <returns><c>true</c> quando parece um segredo.</returns>
    public static bool Credencial(string candidato)
    {
        ArgumentNullException.ThrowIfNull(candidato);
        if (candidato.StartsWith("eyJ", StringComparison.Ordinal) || candidato.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidato.Length >= TamanhoMinimoDeSegredo
            && !Uuid().IsMatch(candidato)
            && candidato.Count(char.IsAsciiDigit) >= 3
            && candidato.Count(char.IsAsciiLetter) >= 3
            && Entropia(candidato) >= EntropiaMinimaDeSegredo;
    }

    /// <summary>A entropia de Shannon, em bits por caractere.</summary>
    /// <param name="texto">O texto.</param>
    /// <returns>A entropia (0 para texto vazio).</returns>
    public static double Entropia(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (texto.Length == 0)
        {
            return 0;
        }

        return texto.GroupBy(c => c)
            .Select(g => (double)g.Count() / texto.Length)
            .Sum(p => -p * Math.Log2(p));
    }

    private static int[] Digitos(string candidato)
    {
        ArgumentNullException.ThrowIfNull(candidato);
        return [.. candidato.Where(char.IsAsciiDigit).Select(c => c - '0')];
    }

    private static int VerificadorDoCpf(int[] d, int quantidade)
    {
        int soma = 0;
        for (int i = 0; i < quantidade; i++)
        {
            soma += d[i] * (quantidade + 1 - i);
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }

    [GeneratedRegex("^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex Uuid();
}
