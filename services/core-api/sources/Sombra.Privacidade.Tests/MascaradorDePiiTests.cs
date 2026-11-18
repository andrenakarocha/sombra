using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade.Deteccao;

namespace Sombra.Privacidade.Tests;

/// <summary>As máscaras da spec 07, aplicadas pelo formato que cada regra escolhe.</summary>
[TestClass]
public sealed class MascaradorDePiiTests
{
    /// <summary>Cada formato produz a máscara da spec; fora da forma esperada, cai no parcial.</summary>
    /// <param name="formato">O formato.</param>
    /// <param name="trecho">O dado.</param>
    /// <param name="esperado">A máscara.</param>
    [TestMethod]
    [DataRow("cpf", "123.456.789-09", "***.456.***-09")]
    [DataRow("cns", "700 0000 0000 1234", "*** **** **** 1234")]
    [DataRow("telefone", "(11) 98765-4321", "(11) *****-4321")]
    [DataRow("telefone", "+55 11 98765-4321", "(11) *****-4321")]
    [DataRow("email", "maria@exemplo.com.br", "m***@e***.com.br")]
    [DataRow("saude", "diabetes", MascaradorDePii.SaudeProtegida)]
    [DataRow("removido", "s3nha", MascaradorDePii.Removido)]
    [DataRow("parcial", "12.345.678-9", "**.***.**8-9")]
    [DataRow("parcial", "01310-100", "*****-*00")]
    [DataRow("parcial", "123", "***")]
    [DataRow("cpf", "não informado", "*** *******do")]
    public void AplicaOFormato(string formato, string trecho, string esperado)
        => Assert.AreEqual(esperado, MascaradorDePii.Aplicar(formato, trecho));

    /// <summary>Formato desconhecido é erro de programação, não máscara vazia.</summary>
    [TestMethod]
    public void FormatoDesconhecidoFalha()
        => Assert.ThrowsExactly<ArgumentException>(() => MascaradorDePii.Aplicar("borrar", "x"));

    /// <summary>No cabeçalho Bearer, só o token é o dado: a palavra Bearer fica.</summary>
    [TestMethod]
    public void BearerMantemOEsquema()
        => Assert.AreEqual($"Authorization: Bearer {MascaradorDePii.Removido}", MotorDePii.Padrao.Mascarar($"Authorization: Bearer {CasosDePii.SegredoSintetico(9, 40)}"));

    /// <summary>O motor mascara cada ocorrência pela regra dela, mantendo o resto do texto.</summary>
    [TestMethod]
    public void MotorMascaraCadaOcorrenciaPelaRegra()
    {
        string cpf = DocumentosSinteticos.Cpf(31);

        string mascarado = MotorDePii.Padrao.Mascarar($"Ligar (11) 98765-4321 ou maria@exemplo.com, CPF {cpf}. Toma insulina.");

        Assert.AreEqual(
            $"Ligar (11) *****-4321 ou m***@e***.com, CPF ***.{cpf[3..6]}.***-{cpf[9..]}. Toma {MascaradorDePii.SaudeProtegida}.",
            mascarado);
    }
}
