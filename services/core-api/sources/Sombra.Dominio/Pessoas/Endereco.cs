using System.Collections.Frozen;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Pessoas;

/// <summary>
/// Endereço de um domicílio. É dado pessoal: só o <see cref="Bairro"/> aparece em telas agregadas (painel), e o
/// <see cref="ToString"/> nunca mostra o endereço.
/// </summary>
public sealed record Endereco
{
    private static readonly FrozenSet<string> _ufs = FrozenSet.ToFrozenSet(
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ",
        "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ]);

    private Endereco(string logradouro, string numero, string bairro, string cidade, string uf, string cep)
    {
        Logradouro = logradouro;
        Numero = numero;
        Bairro = bairro;
        Cidade = cidade;
        Uf = uf;
        Cep = cep;
    }

    /// <summary>Gets a rua, avenida ou travessa.</summary>
    public string Logradouro { get; }

    /// <summary>Gets o número (pode ter letra, como <c>12A</c>).</summary>
    public string Numero { get; }

    /// <summary>Gets o bairro.</summary>
    public string Bairro { get; }

    /// <summary>Gets a cidade.</summary>
    public string Cidade { get; }

    /// <summary>Gets a sigla da UF, em maiúsculas.</summary>
    public string Uf { get; }

    /// <summary>Gets o CEP, 8 dígitos.</summary>
    public string Cep { get; }

    /// <summary>Cria um endereço, conferindo os campos obrigatórios, a UF e o CEP.</summary>
    /// <param name="logradouro">A rua, avenida ou travessa.</param>
    /// <param name="numero">O número.</param>
    /// <param name="bairro">O bairro.</param>
    /// <param name="cidade">A cidade.</param>
    /// <param name="uf">A sigla da UF.</param>
    /// <param name="cep">O CEP, com ou sem hífen.</param>
    /// <returns>O endereço válido.</returns>
    /// <exception cref="RegraVioladaException">Quando falta um campo ou a UF ou o CEP são inválidos.</exception>
    public static Endereco Criar(string? logradouro, string? numero, string? bairro, string? cidade, string? uf, string? cep)
    {
        if (string.IsNullOrWhiteSpace(logradouro) || string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(bairro)
            || string.IsNullOrWhiteSpace(cidade))
        {
            throw new RegraVioladaException("CADASTRO", "Endereço incompleto.");
        }

        string siglaUf = (uf ?? string.Empty).Trim().ToUpperInvariant();
        if (!_ufs.Contains(siglaUf))
        {
            throw new RegraVioladaException("CADASTRO", "UF inválida.");
        }

        string digitosCep = Digitos.Extrair(cep);
        if (digitosCep.Length != 8)
        {
            throw new RegraVioladaException("CADASTRO", "CEP inválido: são 8 dígitos.");
        }

        return new Endereco(logradouro.Trim(), numero.Trim(), bairro.Trim(), cidade.Trim(), siglaUf, digitosCep);
    }

    /// <summary>Nunca revela o endereço.</summary>
    /// <returns>O texto fixo <c>[ENDERECO]</c>.</returns>
    public override string ToString() => "[ENDERECO]";
}
