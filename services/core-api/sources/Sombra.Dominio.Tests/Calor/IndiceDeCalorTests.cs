using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Tests.Calor;

/// <summary>
/// O índice de calor e os níveis batem com os 20 vetores compartilhados do contrato (ADR-006), os mesmos que a Sentinela,
/// o Clima, o ML e o simulador rodam.
/// </summary>
[TestClass]
public sealed class IndiceDeCalorTests
{
    private static readonly JsonDocument _contrato = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contratos", "indice-de-calor.vetores.json")));

    /// <summary>Gets os vetores do contrato: temperatura, umidade, índice esperado, nível esperado e o caso.</summary>
    public static IEnumerable<object[]> Vetores => _contrato.RootElement.GetProperty("vetores").EnumerateArray()
        .Select(v => new object[]
        {
            v.GetProperty("temperaturaC").GetDouble(),
            v.GetProperty("umidadePct").GetDouble(),
            v.GetProperty("indiceDeCalorC").GetDouble(),
            v.GetProperty("nivel").GetString()!,
            v.GetProperty("caso").GetString()!,
        });

    /// <summary>Cada vetor do contrato dá o mesmo índice (com a tolerância do contrato) e o mesmo nível.</summary>
    /// <param name="temperatura">A temperatura, em °C.</param>
    /// <param name="umidade">A umidade relativa, em %.</param>
    /// <param name="indiceEsperado">O índice esperado, em °C.</param>
    /// <param name="nivelEsperado">O código do nível no contrato.</param>
    /// <param name="caso">A descrição do caso.</param>
    [TestMethod]
    [DynamicData(nameof(Vetores))]
    public void BateComOsVetoresDoContrato(double temperatura, double umidade, double indiceEsperado, string nivelEsperado, string caso)
    {
        double tolerancia = _contrato.RootElement.GetProperty("toleranciaC").GetDouble();
        IndiceDeCalor indice = IndiceDeCalor.Calcular(Temperatura.Criar(temperatura), UmidadeRelativa.Criar(umidade));

        Assert.AreEqual(indiceEsperado, indice.Celsius, tolerancia, caso);
        Assert.AreEqual(nivelEsperado, CodigoDoContrato(indice.NivelPadrao), caso);
    }

    /// <summary>Os limites padrão são os do contrato, e a classificação é fechada embaixo e aberta em cima.</summary>
    [TestMethod]
    public void LimitesPadraoSaoOsDoContrato()
    {
        JsonElement limiares = _contrato.RootElement.GetProperty("limiaresC");

        Assert.AreEqual(limiares.GetProperty("ATENCAO").GetDouble(), LimitesTermicos.Padrao.Atencao);
        Assert.AreEqual(limiares.GetProperty("CAUTELA_EXTREMA").GetDouble(), LimitesTermicos.Padrao.CautelaExtrema);
        Assert.AreEqual(limiares.GetProperty("PERIGO").GetDouble(), LimitesTermicos.Padrao.Perigo);
        Assert.AreEqual(limiares.GetProperty("PERIGO_EXTREMO").GetDouble(), LimitesTermicos.Padrao.PerigoExtremo);
    }

    /// <summary>Regra R3: a redução pessoal baixa os limites a partir de cautela extrema e mantém o de atenção.</summary>
    [TestMethod]
    public void ReducaoPessoalAntecipaOPerigo()
    {
        IndiceDeCalor indice = IndiceDeCalor.Calcular(Temperatura.Criar(32.0), UmidadeRelativa.Criar(60.0));
        LimitesTermicos pessoal = LimitesTermicos.ComReducao(5.0);

        Assert.AreEqual(NivelDeRisco.CautelaExtrema, LimitesTermicos.Padrao.Classificar(indice));
        Assert.AreEqual(NivelDeRisco.Perigo, pessoal.Classificar(indice));
        Assert.AreEqual(LimitesTermicos.Padrao.Atencao, pessoal.Atencao);
    }

    /// <summary>Regra R3: redução negativa ou que não é número é recusada (não há teto: D17).</summary>
    /// <param name="graus">A redução pedida.</param>
    [TestMethod]
    [DataRow(-0.5)]
    [DataRow(double.NaN)]
    public void RecusaReducaoForaDaFaixa(double graus)
        => Assert.AreEqual("R3", Assert.ThrowsExactly<RegraVioladaException>(() => LimitesTermicos.ComReducao(graus)).Regra);

    /// <summary>Regra R9: temperatura e umidade fora da faixa física da Sentinela são recusadas.</summary>
    [TestMethod]
    public void RecusaLeituraForaDaFaixaFisica()
    {
        Assert.AreEqual("R9", Assert.ThrowsExactly<RegraVioladaException>(() => Temperatura.Criar(60.1)).Regra);
        Assert.AreEqual("R9", Assert.ThrowsExactly<RegraVioladaException>(() => Temperatura.Criar(-10.1)).Regra);
        Assert.AreEqual("R9", Assert.ThrowsExactly<RegraVioladaException>(() => UmidadeRelativa.Criar(100.1)).Regra);
        Assert.AreEqual("R9", Assert.ThrowsExactly<RegraVioladaException>(() => UmidadeRelativa.Criar(double.NaN)).Regra);
        Assert.AreEqual(60.0, Temperatura.Criar(Temperatura.Maxima).Celsius);
    }

    private static string CodigoDoContrato(NivelDeRisco nivel) => nivel switch
    {
        NivelDeRisco.Normal => "NORMAL",
        NivelDeRisco.Atencao => "ATENCAO",
        NivelDeRisco.CautelaExtrema => "CAUTELA_EXTREMA",
        NivelDeRisco.Perigo => "PERIGO",
        NivelDeRisco.PerigoExtremo => "PERIGO_EXTREMO",
        _ => throw new ArgumentOutOfRangeException(nameof(nivel)),
    };
}
