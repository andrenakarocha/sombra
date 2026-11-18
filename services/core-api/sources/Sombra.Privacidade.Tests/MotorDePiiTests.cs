using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Tests;

/// <summary>O motor de PII com as regras padrão: a tabela inteira e as garantias que a tabela não mostra.</summary>
[TestClass]
public sealed class MotorDePiiTests
{
    private static readonly MotorDePii _motor = MotorDePii.Padrao;

    /// <summary>Cada caso da tabela dispara exatamente as regras esperadas: sem falso negativo e sem falso positivo.</summary>
    /// <param name="caso">O caso.</param>
    [TestMethod]
    [DynamicData(nameof(CasosDePii.ComoLinhas), typeof(CasosDePii))]
    public void CasoDaTabela(CasoDePii caso)
    {
        ArgumentNullException.ThrowIfNull(caso);
        string[] obtido = [.. _motor.Analisar(caso.Texto, caso.Chave).Select(o => o.RegraId).Distinct().Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(caso.Esperado.Order(StringComparer.Ordinal).ToArray(), obtido, $"{caso}: obtido [{string.Join(", ", obtido)}]");
    }

    /// <summary>A tabela cobre todas as regras, e cada regra tem positivo e negativo (o negativo pode estar em outra família).</summary>
    [TestMethod]
    public void TabelaCobreTodasAsRegras()
    {
        string[] semPositivo = [.. _motor.Regras.Select(r => r.Id).Where(id => !CasosDePii.Todos.Any(c => c.Tipo == CasosDePii.Positivo && c.Esperado.Contains(id)))];

        Assert.IsEmpty(semPositivo, $"regras sem caso positivo: {string.Join(", ", semPositivo)}");
        Assert.IsGreaterThanOrEqualTo(15, CasosDePii.Todos.Count(c => c.Tipo == CasosDePii.Negativo));
        Assert.IsGreaterThanOrEqualTo(10, CasosDePii.Todos.Count(c => c.Tipo == CasosDePii.Dificil));
    }

    /// <summary>A ocorrência nunca carrega o valor: só regra, categoria, severidade e posição.</summary>
    [TestMethod]
    public void OcorrenciaNaoCarregaOValor()
    {
        string cpf = DocumentosSinteticos.Cpf(11);
        string texto = $"CPF {cpf}";
        OcorrenciaDePii ocorrencia = _motor.Analisar(texto).Single();

        Assert.DoesNotContain(cpf, ocorrencia.ToString());
        Assert.AreEqual(cpf, texto.Substring(ocorrencia.Inicio, ocorrencia.Tamanho));
    }

    /// <summary>Quando o nome do campo denuncia o dado, o valor inteiro vira uma ocorrência só, da regra mais severa.</summary>
    [TestMethod]
    public void ChaveCobreOValorInteiroComARegraMaisSevera()
    {
        IReadOnlyList<OcorrenciaDePii> ocorrencias = _motor.Analisar("abc (11) 98765-4321", "senhaDoTelefone");

        Assert.HasCount(1, ocorrencias);
        Assert.AreEqual("credencial", ocorrencias[0].RegraId);
        Assert.AreEqual(OrigemDaDeteccao.Chave, ocorrencias[0].Origem);
        Assert.AreEqual(0, ocorrencias[0].Inicio);
        Assert.AreEqual(19, ocorrencias[0].Tamanho);
    }

    /// <summary>Um CPF válido que também parece telefone fica com a regra mais severa (CPF), sem duas máscaras no mesmo trecho.</summary>
    [TestMethod]
    public void SobreposicaoFicaComARegraMaisSevera()
    {
        string cpf = Enumerable.Range(1, 5000).Select(DocumentosSinteticos.Cpf).First(Validadores.Telefone);

        OcorrenciaDePii ocorrencia = _motor.Analisar($"número {cpf}").Single();

        Assert.AreEqual("cpf", ocorrencia.RegraId);
    }

    /// <summary>As posições valem no texto normalizado, mesmo quando a entrada chega decomposta (NFD).</summary>
    [TestMethod]
    public void PosicoesValemNoTextoNormalizado()
    {
        string decomposto = "Análise: diagnóstico";
        string normalizado = MotorDePii.Normalizar(decomposto);

        OcorrenciaDePii ocorrencia = _motor.Analisar(decomposto).Single();

        Assert.AreEqual("diagnóstico", normalizado.Substring(ocorrencia.Inicio, ocorrencia.Tamanho));
    }

    /// <summary>Um payload de 2 MB com o CPF no fim é analisado inteiro e rápido (as regex são lineares e têm timeout).</summary>
    [TestMethod]
    public void PayloadEnormeContinuaRapidoEInteiro()
    {
        string cpf = DocumentosSinteticos.Cpf(23);
        string texto = string.Concat(Enumerable.Repeat("texto comum de observação, 41,3 °C às 14h; ", 50_000)) + cpf;

        Stopwatch relogio = Stopwatch.StartNew();
        IReadOnlyList<OcorrenciaDePii> ocorrencias = _motor.Analisar(texto);
        relogio.Stop();

        Assert.AreEqual("cpf", ocorrencias.Single().RegraId);
        Assert.IsLessThan(10_000, relogio.ElapsedMilliseconds, $"levou {relogio.ElapsedMilliseconds} ms");
    }

    /// <summary>As máscaras e severidades das regras padrão batem com a spec 07.</summary>
    /// <param name="id">A regra.</param>
    /// <param name="categoria">A categoria esperada.</param>
    /// <param name="mascara">A máscara esperada.</param>
    [TestMethod]
    [DataRow("cpf", CategoriaDePii.Identificacao, "cpf")]
    [DataRow("cns", CategoriaDePii.Saude, "cns")]
    [DataRow("telefone", CategoriaDePii.Contato, "telefone")]
    [DataRow("email", CategoriaDePii.Contato, "email")]
    [DataRow("saude", CategoriaDePii.Saude, "saude")]
    [DataRow("credencial", CategoriaDePii.Credencial, "removido")]
    public void RegrasPadraoSeguemASpec(string id, CategoriaDePii categoria, string mascara)
    {
        RegraDePii regra = _motor.Regras.Single(r => r.Id == id);

        Assert.AreEqual(categoria, regra.Categoria);
        Assert.AreEqual(mascara, regra.Mascara);
        StringAssert.StartsWith(regra.BaseLegal, "LGPD art.");
    }
}
