using System;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Privacidade.Tests;

/// <summary>A camada antes das saídas: taint, conteúdo, JSON campo a campo e o <see cref="Sensivel{T}"/>.</summary>
[TestClass]
public sealed class GuardaDeSaidaTests
{
    private static readonly GuardaDeSaida _guarda = new(MotorDePii.Padrao);

    /// <summary>O que entrou marcado é reconhecido na saída, mesmo sem pontuação, e redigido como <c>[PII:regra]</c>.</summary>
    [TestMethod]
    public void TaintReconheceODadoQueEntrou()
    {
        string cpf = DocumentosSinteticos.Cpf(51);
        ContextoDeTaint taint = new();
        new VarredorDePii(MotorDePii.Padrao, PoliticaDeCampoLivre.Mascarar).MarcarValor("minha-senha-forte", "senha", FonteDePii.Query, taint);
        taint.MarcarValor(cpf, "cpf", CategoriaDePii.Identificacao);

        ResultadoDaGuarda r = _guarda.Verificar($"falha ao gravar {cpf} com minha-senha-forte", taint);

        Assert.IsTrue(r.Achados.All(a => a.PeloTaint));
        Assert.AreEqual("falha ao gravar [PII:cpf] com [PII:credencial]", r.Redigido);
    }

    /// <summary>Um JSON é conferido campo a campo: o telefone no texto é redigido e a assinatura em <c>sig</c> fica.</summary>
    [TestMethod]
    public void JsonCampoACampoRespeitaAsExcecoesPorChave()
    {
        string sig = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CasosDePii.SegredoSintetico(21, 32)));
        ResultadoDaGuarda r = _guarda.Verificar($$"""{"sig":"{{sig}}","aviso":"ligar (11) 98765-4321"}""", null);

        Assert.AreEqual("telefone", r.Achados.Single().RegraId);
        JsonElement redigido = JsonDocument.Parse(r.Redigido).RootElement;
        Assert.AreEqual(sig, redigido.GetProperty("sig").GetString());
        Assert.AreEqual("ligar [PII:telefone]", redigido.GetProperty("aviso").GetString());
    }

    /// <summary>Com o nome do campo (a propriedade do log), a regra pela chave vale: o valor inteiro sai redigido.</summary>
    [TestMethod]
    public void ChaveDoCampoRedigeOValorInteiro()
        => Assert.AreEqual("[PII:cpf]", _guarda.Verificar("não informado", null, chave: "cpfDoTitular").Redigido);

    /// <summary>Conteúdo limpo passa intacto, e as famílias permitidas não bloqueiam.</summary>
    [TestMethod]
    public void LimpoPassaEPermitidasNaoBloqueiam()
    {
        ResultadoDaGuarda limpo = _guarda.Verificar("""{"nivel":"PERIGO","indice":41.3,"casa":"0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10"}""", null);
        ResultadoDaGuarda telefone = _guarda.Verificar("(11) 98765-4321", null);

        Assert.IsTrue(limpo.Limpo);
        Assert.IsEmpty(telefone.Fora([CategoriaDePii.Contato]));
        Assert.HasCount(1, telefone.Fora([CategoriaDePii.Saude]));
    }

    /// <summary><see cref="Sensivel{T}"/>: some em texto, recusa JSON e só sai com finalidade.</summary>
    [TestMethod]
    public void SensivelSomeEmTextoRecusaJsonESoSaiComFinalidade()
    {
        Sensivel<string> cpf = new(DocumentosSinteticos.Cpf(52), CategoriaDePii.Identificacao);

        Assert.AreEqual("[PII:IDENTIFICACAO]", cpf.ToString());
        Assert.AreEqual("cpf [PII:IDENTIFICACAO]", $"cpf {cpf}");
        Assert.ThrowsExactly<InvalidOperationException>(() => JsonSerializer.Serialize(new { cpf }));
        Assert.ThrowsExactly<ArgumentException>(() => cpf.Revelar(" "));
        Assert.AreEqual(DocumentosSinteticos.Cpf(52), cpf.Revelar("teste"));
    }
}
