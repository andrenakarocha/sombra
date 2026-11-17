using System.Globalization;
using System.Linq;

namespace Sombra.Dominio.Tests.Apoio;

/// <summary>
/// Gera CPF e Cartão SUS sintéticos para os testes, com uma implementação independente da do domínio. Nenhum número
/// aqui vem de uma pessoa real.
/// </summary>
internal static class DocumentosSinteticos
{
    /// <summary>Gera um CPF válido (11 dígitos) a partir de uma semente.</summary>
    /// <param name="semente">Qualquer número; sementes diferentes geram CPFs diferentes.</param>
    /// <returns>Os 11 dígitos.</returns>
    internal static string Cpf(int semente)
    {
        int[] digitos = BaseNumerica(semente, 9).ToArray();
        int primeiro = VerificadorCpf(digitos);
        int segundo = VerificadorCpf([.. digitos, primeiro]);
        return string.Concat(digitos.Select(d => d.ToString(CultureInfo.InvariantCulture))) + primeiro + segundo;
    }

    /// <summary>Gera um Cartão SUS provisório (começa em 7) válido a partir de uma semente.</summary>
    /// <param name="semente">Qualquer número.</param>
    /// <returns>Os 15 dígitos.</returns>
    internal static string CartaoSus(int semente)
    {
        for (int tentativa = semente; ; tentativa++)
        {
            int[] digitos = [7, .. BaseNumerica(tentativa, 13)];
            int soma = digitos.Select((d, i) => d * (15 - i)).Sum();
            int ultimo = (11 - (soma % 11)) % 11;
            if (ultimo < 10)
            {
                return string.Concat(digitos.Select(d => d.ToString(CultureInfo.InvariantCulture))) + ultimo;
            }
        }
    }

    private static int[] BaseNumerica(int semente, int quantidade)
    {
        int[] digitos = new int[quantidade];
        uint estado = (uint)semente * 2_654_435_761u;
        for (int i = 0; i < quantidade; i++)
        {
            estado = (estado * 1_103_515_245u) + 12_345u;
            digitos[i] = (int)((estado >> 16) % 10u);
        }

        if (digitos.Distinct().Count() == 1)
        {
            digitos[0] = (digitos[0] + 1) % 10;
        }

        return digitos;
    }

    private static int VerificadorCpf(int[] digitos)
    {
        int peso = digitos.Length + 1;
        int soma = digitos.Select((d, i) => d * (peso - i)).Sum();
        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
