using System.Linq;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Pessoas;

/// <summary>
/// Cartão Nacional de Saúde (CNS), validado pelo algoritmo oficial (regra R1): 15 dígitos, começando em 1, 2, 7, 8 ou 9,
/// e a soma ponderada (pesos 15 a 1) divisível por 11. É dado pessoal de saúde: o <see cref="ToString"/> nunca o mostra.
/// </summary>
public sealed record CartaoSus
{
    private const string PrimeirosDigitosValidos = "12789";

    private CartaoSus(string valor) => Valor = valor;

    /// <summary>Gets os 15 dígitos, sem espaços.</summary>
    public string Valor { get; }

    /// <summary>Cria um Cartão SUS a partir do texto digitado, com ou sem espaços.</summary>
    /// <param name="texto">O número digitado.</param>
    /// <returns>O cartão válido.</returns>
    /// <exception cref="RegraVioladaException">Quando o número não passa na validação oficial.</exception>
    public static CartaoSus Criar(string? texto)
    {
        string digitos = Digitos.Extrair(texto);
        if (!EhValido(digitos))
        {
            throw new RegraVioladaException("R1", "Cartão SUS inválido.");
        }

        return new CartaoSus(digitos);
    }

    /// <summary>Diz se 15 dígitos formam um Cartão SUS válido.</summary>
    /// <param name="digitos">Os dígitos, sem espaços.</param>
    /// <returns><c>true</c> quando o primeiro dígito e a soma ponderada são válidos.</returns>
    public static bool EhValido(string digitos)
    {
        if (digitos is null || digitos.Length != 15 || !digitos.All(char.IsAsciiDigit)
            || !PrimeirosDigitosValidos.Contains(digitos[0], System.StringComparison.Ordinal))
        {
            return false;
        }

        int soma = 0;
        for (int i = 0; i < 15; i++)
        {
            soma += Digitos.Valor(digitos[i]) * (15 - i);
        }

        return soma % 11 == 0;
    }

    /// <summary>Nunca revela o número: é dado de saúde (LGPD, art. 11).</summary>
    /// <returns>O texto fixo <c>[CARTAO_SUS]</c>.</returns>
    public override string ToString() => "[CARTAO_SUS]";
}
