using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Dominio.Tests.Sentinelas;

/// <summary>
/// O código de vínculo do QR bate com os vetores compartilhados (<c>tools/vinculo/codigo.py</c>), que o simulador e o
/// firmware vão usar.
/// </summary>
[TestClass]
public sealed class CodigoDeVinculoTests
{
    private static readonly JsonDocument _contrato = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "vinculo-da-sentinela.vetores.json")));

    private static readonly byte[] _chave = Convert.FromHexString(_contrato.RootElement.GetProperty("chaveHex").GetString()!);

    /// <summary>Gets os vetores do contrato: a Sentinela e o código esperado.</summary>
    public static IEnumerable<object[]> Vetores => _contrato.RootElement.GetProperty("vetores").EnumerateArray()
        .Select(v => new object[] { v.GetProperty("deviceId").GetString()!, v.GetProperty("codigo").GetString()! });

    /// <summary>Cada vetor dá o mesmo código, só com o alfabeto do app.</summary>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="codigo">O código esperado.</param>
    [TestMethod]
    [DynamicData(nameof(Vetores))]
    public void BateComOsVetores(string deviceId, string codigo)
    {
        Assert.AreEqual(codigo, CodigoDeVinculo.Derivar(deviceId, _chave));
        Assert.IsTrue(codigo.All(c => CodigoDeVinculo.Alfabeto.Contains(c, StringComparison.Ordinal)));
        Assert.AreEqual(CodigoDeVinculo.Alfabeto, _contrato.RootElement.GetProperty("alfabeto").GetString());
    }

    /// <summary>Confere sem diferenciar maiúsculas; outro código, outra Sentinela ou outra chave não passam.</summary>
    [TestMethod]
    public void ConfereSoOCodigoCerto()
    {
        string codigo = CodigoDeVinculo.Derivar("snt-abc123", _chave);
        byte[] outraChave = new byte[32];

        Assert.IsTrue(CodigoDeVinculo.Confere("snt-abc123", $" {codigo.ToLowerInvariant()} ", _chave));
        Assert.IsFalse(CodigoDeVinculo.Confere("snt-abc124", codigo, _chave));
        Assert.IsFalse(CodigoDeVinculo.Confere("snt-abc123", codigo, outraChave));
        Assert.IsFalse(CodigoDeVinculo.Confere("snt-abc123", "AAAAAA", _chave));
        Assert.IsFalse(CodigoDeVinculo.Confere("snt-abc123", null, _chave));
    }
}
