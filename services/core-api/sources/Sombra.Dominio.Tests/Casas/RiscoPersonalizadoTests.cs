using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Dominio.Tests.Casas;

/// <summary>
/// A regra R3 do Core é a do app (D17): os mesmos vetores de <c>docs/contratos/risco-personalizado.vetores.json</c> passam
/// no Jest do app e aqui.
/// </summary>
[TestClass]
public sealed class RiscoPersonalizadoTests
{
    private static readonly JsonDocument _contrato = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "risco-personalizado.vetores.json")));

    /// <summary>Gets os vetores: fatores declarados, idade e o vetor inteiro.</summary>
    public static IEnumerable<object[]> Vetores => _contrato.RootElement.GetProperty("vetores").EnumerateArray()
        .Select(v => new object[] { string.Join('+', v.GetProperty("fatoresDeclarados").EnumerateArray().Select(f => f.GetString())), v.GetProperty("idade").GetInt32(), v.GetRawText() });

    /// <summary>As reduções por fator são as do app.</summary>
    [TestMethod]
    public void ReducoesSaoAsDoApp()
    {
        foreach (JsonProperty fator in _contrato.RootElement.GetProperty("reducaoPorFatorC").EnumerateObject())
        {
            Assert.AreEqual(fator.Value.GetDouble(), FatorDeRisco.DoCodigo(fator.Name).ReducaoGraus, fator.Name);
        }
    }

    /// <summary>Cada combinação de fatores e idade dá os mesmos fatores, limiares e níveis que o app.</summary>
    /// <param name="fatores">Os fatores declarados (para o nome do caso).</param>
    /// <param name="idade">A idade.</param>
    /// <param name="json">O vetor.</param>
    [TestMethod]
    [DynamicData(nameof(Vetores))]
    public void BateComOsVetoresDoApp(string fatores, int idade, string json)
    {
        JsonElement vetor = JsonDocument.Parse(json).RootElement;
        FatorDeRisco[] declarados = [.. vetor.GetProperty("fatoresDeclarados").EnumerateArray().Select(f => FatorDeRisco.DoCodigo(f.GetString()))];
        Morador morador = Fabricas.Morador(idade, 1, declarados);
        LimitesTermicos limites = morador.LimitesTermicos(Fabricas.Hoje);
        JsonElement esperado = vetor.GetProperty("limiares");

        CollectionAssert.AreEquivalent(
            vetor.GetProperty("fatoresQueValem").EnumerateArray().Select(f => f.GetString()).ToArray(),
            morador.FatoresDeRisco(Fabricas.Hoje).Select(f => f.Codigo).ToArray(),
            fatores);
        Assert.AreEqual(esperado.GetProperty("ATENCAO").GetDouble(), limites.Atencao);
        Assert.AreEqual(esperado.GetProperty("CAUTELA_EXTREMA").GetDouble(), limites.CautelaExtrema);
        Assert.AreEqual(esperado.GetProperty("PERIGO").GetDouble(), limites.Perigo);
        Assert.AreEqual(esperado.GetProperty("PERIGO_EXTREMO").GetDouble(), limites.PerigoExtremo);
        foreach (JsonElement amostra in vetor.GetProperty("classificacoes").EnumerateArray())
        {
            Assert.AreEqual(amostra.GetProperty("nivel").GetString(), Codigo(limites.Classificar(amostra.GetProperty("indiceC").GetDouble())));
        }
    }

    private static string Codigo(NivelDeRisco nivel) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(nivel.ToString());
}
