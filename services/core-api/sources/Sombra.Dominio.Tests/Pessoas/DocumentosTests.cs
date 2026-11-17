using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Dominio.Tests.Pessoas;

/// <summary>Regra R1: CPF e Cartão SUS só existem se passarem na validação oficial, e nunca aparecem em texto.</summary>
[TestClass]
public sealed class DocumentosTests
{
    /// <summary>Cem CPFs sintéticos válidos são aceitos, com ou sem pontuação.</summary>
    [TestMethod]
    public void AceitaCpfsValidosComOuSemPontuacao()
    {
        for (int semente = 1; semente <= 100; semente++)
        {
            string digitos = DocumentosSinteticos.Cpf(semente);
            string pontuado = string.Create(CultureInfo.InvariantCulture, $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}");

            Assert.AreEqual(digitos, Cpf.Criar(digitos).Valor);
            Assert.AreEqual(digitos, Cpf.Criar(pontuado).Valor);
        }
    }

    /// <summary>Trocar qualquer verificador, repetir um dígito só ou errar o tamanho invalida o CPF.</summary>
    /// <param name="texto">O CPF digitado.</param>
    [TestMethod]
    [DataRow("111.111.111-11")]
    [DataRow("000.000.000-00")]
    [DataRow("1234567890")]
    [DataRow("123456789012")]
    [DataRow("")]
    [DataRow(null)]
    public void RecusaCpfInvalido(string? texto)
    {
        RegraVioladaException erro = Assert.ThrowsExactly<RegraVioladaException>(() => Cpf.Criar(texto));
        Assert.AreEqual("R1", erro.Regra);
    }

    /// <summary>Qualquer verificador errado invalida um CPF que seria válido.</summary>
    [TestMethod]
    public void RecusaCpfComVerificadorErrado()
    {
        string valido = DocumentosSinteticos.Cpf(42);
        string errado = valido[..10] + ((valido[10] - '0' + 1) % 10).ToString(CultureInfo.InvariantCulture);

        Assert.IsTrue(Cpf.EhValido(valido));
        Assert.IsFalse(Cpf.EhValido(errado));
    }

    /// <summary>Cem Cartões SUS sintéticos válidos são aceitos, com ou sem espaços.</summary>
    [TestMethod]
    public void AceitaCartoesSusValidos()
    {
        for (int semente = 1; semente <= 100; semente++)
        {
            string digitos = DocumentosSinteticos.CartaoSus(semente);
            string comEspacos = string.Create(CultureInfo.InvariantCulture, $"{digitos[..3]} {digitos[3..7]} {digitos[7..11]} {digitos[11..]}");

            Assert.AreEqual(digitos, CartaoSus.Criar(comEspacos).Valor);
        }
    }

    /// <summary>Primeiro dígito fora de 1, 2, 7, 8 ou 9, soma errada ou tamanho errado invalidam o Cartão SUS.</summary>
    [TestMethod]
    public void RecusaCartaoSusInvalido()
    {
        string valido = DocumentosSinteticos.CartaoSus(7);
        string somaErrada = valido[..14] + ((valido[14] - '0' + 1) % 10).ToString(CultureInfo.InvariantCulture);
        string primeiroInvalido = "3" + valido[1..];

        Assert.IsFalse(CartaoSus.EhValido(somaErrada));
        Assert.IsFalse(CartaoSus.EhValido(primeiroInvalido));
        Assert.IsFalse(CartaoSus.EhValido(valido[..14]));
        Assert.AreEqual("R1", Assert.ThrowsExactly<RegraVioladaException>(() => CartaoSus.Criar(somaErrada)).Regra);
    }

    /// <summary>Um documento nunca vaza por <c>ToString</c>, nem dentro de uma interpolação.</summary>
    [TestMethod]
    public void DocumentosNuncaAparecemEmTexto()
    {
        Cpf cpf = Cpf.Criar(DocumentosSinteticos.Cpf(3));
        CartaoSus cartao = CartaoSus.Criar(DocumentosSinteticos.CartaoSus(3));
        string log = string.Create(CultureInfo.InvariantCulture, $"cpf={cpf} cns={cartao}");

        Assert.AreEqual("cpf=[CPF] cns=[CARTAO_SUS]", log);
        Assert.IsFalse(log.Contains(cpf.Valor, System.StringComparison.Ordinal));
    }
}
