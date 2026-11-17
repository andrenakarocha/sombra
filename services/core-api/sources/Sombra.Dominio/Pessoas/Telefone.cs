using System.Globalization;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Pessoas;

/// <summary>
/// Telefone brasileiro com DDD: fixo (10 dígitos) ou celular (11 dígitos, começando em 9). É dado pessoal: o
/// <see cref="ToString"/> nunca mostra o número.
/// </summary>
public sealed record Telefone
{
    private Telefone(string valor) => Valor = valor;

    /// <summary>Gets o DDD e o número, só dígitos (ex.: <c>11987654321</c>).</summary>
    public string Valor { get; }

    /// <summary>Gets o número no formato de discagem, <c>(11) 98765-4321</c>.</summary>
    public string Formatado => Valor.Length == 11
        ? string.Create(CultureInfo.InvariantCulture, $"({Valor[..2]}) {Valor[2..7]}-{Valor[7..]}")
        : string.Create(CultureInfo.InvariantCulture, $"({Valor[..2]}) {Valor[2..6]}-{Valor[6..]}");

    /// <summary>Cria um telefone a partir do texto digitado.</summary>
    /// <param name="texto">O telefone com DDD, com ou sem pontuação.</param>
    /// <returns>O telefone válido.</returns>
    /// <exception cref="RegraVioladaException">Quando o DDD ou a quantidade de dígitos não fecham.</exception>
    public static Telefone Criar(string? texto)
    {
        string digitos = Digitos.Extrair(texto);
        bool dddValido = digitos.Length >= 2 && digitos[0] != '0' && digitos[1] != '0';
        bool celularValido = digitos.Length == 11 && digitos[2] == '9';
        if (!dddValido || (digitos.Length != 10 && !celularValido))
        {
            throw new RegraVioladaException("CADASTRO", "Telefone inválido: use DDD e número.");
        }

        return new Telefone(digitos);
    }

    /// <summary>Nunca revela o número.</summary>
    /// <returns>O texto fixo <c>[TELEFONE]</c>.</returns>
    public override string ToString() => "[TELEFONE]";
}
