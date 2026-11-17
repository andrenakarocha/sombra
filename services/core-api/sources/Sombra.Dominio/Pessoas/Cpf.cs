using System.Linq;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Pessoas;

/// <summary>
/// CPF validado pelos dígitos verificadores (regra R1). É dado pessoal: o <see cref="ToString"/> nunca mostra o número.
/// </summary>
public sealed record Cpf
{
    private Cpf(string valor) => Valor = valor;

    /// <summary>Gets os 11 dígitos, sem pontuação.</summary>
    public string Valor { get; }

    /// <summary>Cria um CPF a partir do texto digitado, com ou sem pontuação.</summary>
    /// <param name="texto">O CPF digitado.</param>
    /// <returns>O CPF válido.</returns>
    /// <exception cref="RegraVioladaException">Quando não há 11 dígitos ou os verificadores não batem.</exception>
    public static Cpf Criar(string? texto)
    {
        string digitos = Digitos.Extrair(texto);
        if (!EhValido(digitos))
        {
            throw new RegraVioladaException("R1", "CPF inválido.");
        }

        return new Cpf(digitos);
    }

    /// <summary>Diz se 11 dígitos formam um CPF válido.</summary>
    /// <param name="digitos">Os dígitos, sem pontuação.</param>
    /// <returns><c>true</c> quando os dois verificadores batem e os dígitos não são todos iguais.</returns>
    public static bool EhValido(string digitos)
    {
        if (digitos is null || digitos.Length != 11 || !digitos.All(char.IsAsciiDigit) || digitos.Distinct().Count() == 1)
        {
            return false;
        }

        return Digitos.Valor(digitos[9]) == Verificador(digitos, 9) && Digitos.Valor(digitos[10]) == Verificador(digitos, 10);
    }

    /// <summary>Nunca revela o número: um CPF pode acabar num log por descuido.</summary>
    /// <returns>O texto fixo <c>[CPF]</c>.</returns>
    public override string ToString() => "[CPF]";

    private static int Verificador(string digitos, int quantidade)
    {
        int soma = 0;
        for (int i = 0; i < quantidade; i++)
        {
            soma += Digitos.Valor(digitos[i]) * (quantidade + 1 - i);
        }

        int resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
