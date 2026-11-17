using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;

namespace Sombra.Dominio.Tests.Pessoas;

/// <summary>Telefone e endereço: formatos brasileiros e nada de dado pessoal em texto.</summary>
[TestClass]
public sealed class ContatoEEnderecoTests
{
    /// <summary>Celular e fixo com DDD são aceitos e formatados para discagem.</summary>
    /// <param name="texto">O telefone digitado.</param>
    /// <param name="formatado">O formato esperado.</param>
    [TestMethod]
    [DataRow("(11) 98765-4321", "(11) 98765-4321")]
    [DataRow("11987654321", "(11) 98765-4321")]
    [DataRow("1132654321", "(11) 3265-4321")]
    public void AceitaTelefoneComDdd(string texto, string formatado)
        => Assert.AreEqual(formatado, Telefone.Criar(texto).Formatado);

    /// <summary>Sem DDD, DDD com zero ou celular sem o 9 são recusados.</summary>
    /// <param name="texto">O telefone digitado.</param>
    [TestMethod]
    [DataRow("98765-4321")]
    [DataRow("01987654321")]
    [DataRow("11887654321")]
    [DataRow("")]
    public void RecusaTelefoneInvalido(string texto)
        => Assert.AreEqual("CADASTRO", Assert.ThrowsExactly<RegraVioladaException>(() => Telefone.Criar(texto)).Regra);

    /// <summary>O endereço normaliza UF e CEP.</summary>
    [TestMethod]
    public void NormalizaUfECep()
    {
        Endereco endereco = Endereco.Criar("Rua das Flores", "12A", "Santana", "São Paulo", " sp ", "02012-000");

        Assert.AreEqual("SP", endereco.Uf);
        Assert.AreEqual("02012000", endereco.Cep);
        Assert.AreEqual("[ENDERECO]", endereco.ToString());
    }

    /// <summary>Campo faltando, UF inexistente ou CEP curto são recusados.</summary>
    /// <param name="uf">A UF digitada.</param>
    /// <param name="cep">O CEP digitado.</param>
    /// <param name="bairro">O bairro digitado.</param>
    [TestMethod]
    [DataRow("XX", "02012000", "Santana")]
    [DataRow("SP", "0201", "Santana")]
    [DataRow("SP", "02012000", " ")]
    public void RecusaEnderecoInvalido(string uf, string cep, string bairro)
        => Assert.ThrowsExactly<RegraVioladaException>(() => Endereco.Criar("Rua das Flores", "12", bairro, "São Paulo", uf, cep));
}
