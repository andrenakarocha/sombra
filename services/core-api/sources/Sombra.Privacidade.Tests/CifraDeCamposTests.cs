using System;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Privacidade.Repouso;

namespace Sombra.Privacidade.Tests;

/// <summary>AES-256-GCM por coluna e índice cego HMAC: o que vai para o banco não abre sem a chave e não é trocável.</summary>
[TestClass]
public sealed class CifraDeCamposTests
{
    private static readonly CifraDeCampos _cifra = new(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));

    /// <summary>Ida e volta, com acento, e o cifrado não contém o valor.</summary>
    [TestMethod]
    public void CifraEDecifra()
    {
        string cifrado = _cifra.Cifrar("Diabetes, toma insulína", "moradores.observacoes_de_saude");

        StringAssert.StartsWith(cifrado, CifraDeCampos.Prefixo);
        Assert.DoesNotContain("Diabetes", cifrado);
        Assert.AreEqual("Diabetes, toma insulína", _cifra.Decifrar(cifrado, "moradores.observacoes_de_saude"));
    }

    /// <summary>Nonce aleatório: o mesmo CPF cifrado duas vezes dá dois textos diferentes (não dá para comparar cifrados).</summary>
    [TestMethod]
    public void MesmoValorGeraCifradosDiferentes()
        => Assert.AreNotEqual(_cifra.Cifrar("12345678909", "moradores.cpf"), _cifra.Cifrar("12345678909", "moradores.cpf"));

    /// <summary>Adulterar um byte, trocar de coluna, usar outra chave ou gravar em claro: tudo falha alto.</summary>
    [TestMethod]
    public void RecusaAdulteradoOutraColunaOutraChaveETextoEmClaro()
    {
        string cifrado = _cifra.Cifrar("12345678909", "moradores.cpf");
        byte[] bruto = Convert.FromBase64String(cifrado[CifraDeCampos.Prefixo.Length..]);
        bruto[^1] ^= 1;
        string adulterado = CifraDeCampos.Prefixo + Convert.ToBase64String(bruto);
        CifraDeCampos outraChave = new(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));

        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _cifra.Decifrar(adulterado, "moradores.cpf"));
        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => _cifra.Decifrar(cifrado, "moradores.cartao_sus"));
        Assert.ThrowsExactly<AuthenticationTagMismatchException>(() => outraChave.Decifrar(cifrado, "moradores.cpf"));
        Assert.ThrowsExactly<CryptographicException>(() => _cifra.Decifrar("12345678909", "moradores.cpf"));
        Assert.ThrowsExactly<CryptographicException>(() => _cifra.Decifrar("v1:%%%", "moradores.cpf"));
    }

    /// <summary>O índice cego é determinístico por chave e por coluna, e não é o valor.</summary>
    [TestMethod]
    public void IndiceCegoEhDeterministicoPorChaveEColuna()
    {
        CifraDeCampos outraChave = new(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));
        string indice = _cifra.IndiceCego("12345678909", "moradores.cpf");

        Assert.AreEqual(indice, _cifra.IndiceCego("12345678909", "moradores.cpf"));
        Assert.AreEqual(64, indice.Length);
        Assert.AreNotEqual(indice, _cifra.IndiceCego("12345678909", "contatos.cpf"));
        Assert.AreNotEqual(indice, outraChave.IndiceCego("12345678909", "moradores.cpf"));
    }

    /// <summary>Chave da configuração: 32 bytes em base64, as duas diferentes; o erro manda rodar o make env.</summary>
    [TestMethod]
    public void ValidaAsChavesDaConfiguracao()
    {
        string boa = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        Assert.IsNotNull(CifraDeCampos.DeBase64(boa, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => CifraDeCampos.DeBase64(null, boa)).Message, "Privacidade:ChaveAes");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => CifraDeCampos.DeBase64(boa, "curta")).Message, "make env");
        Assert.ThrowsExactly<ArgumentException>(() => CifraDeCampos.DeBase64(boa, boa));
    }
}
