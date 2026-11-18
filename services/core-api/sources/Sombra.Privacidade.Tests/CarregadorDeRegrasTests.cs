using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Tests;

/// <summary>O schema do <c>pii-regras.json</c> é validado na carga: regra errada não vira detector mudo.</summary>
[TestClass]
public sealed class CarregadorDeRegrasTests
{
    private static readonly string[] _familiasDaSpec = ["cpf", "cns", "rg", "telefone", "email", "cep", "saude", "credencial"];

    private const string RegraValida = """{"id":"matricula","categoria":"Identificacao","severidade":"Media","padrao":"MAT-\\d{6}","mascara":"parcial","baseLegal":"LGPD art. 5º, I"}""";

    /// <summary>As regras padrão carregam, com ids únicos e as oito famílias da spec.</summary>
    [TestMethod]
    public void RegrasPadraoCarregam()
    {
        string[] ids = [.. CarregadorDeRegras.CarregarPadrao().Select(r => r.Id)];

        CollectionAssert.AreEquivalent(_familiasDaSpec, ids);
    }

    /// <summary>Configurar uma regra nova é só acrescentar um objeto no JSON: o motor passa a detectar.</summary>
    [TestMethod]
    public void RegraNovaPassaADetectarSemMudarCodigo()
    {
        MotorDePii motor = new(CarregadorDeRegras.Carregar($$"""{"versao":1,"regras":[{{RegraValida}}]}"""));

        Assert.AreEqual("matricula", motor.Analisar("aluno MAT-123456 presente").Single().RegraId);
        Assert.IsFalse(motor.ContemPii("aluno MAT-12 presente"));
    }

    /// <summary>Cada violação do schema aparece na mensagem, com o caminho dentro do arquivo.</summary>
    /// <param name="json">O arquivo inválido.</param>
    /// <param name="trecho">O trecho esperado no erro.</param>
    [TestMethod]
    [DataRow("não é json", "JSON malformado")]
    [DataRow("""[]""", "$: precisa ser um objeto")]
    [DataRow("""{"versao":2,"regras":[]}""", "$.versao")]
    [DataRow("""{"versao":1,"regras":[]}""", "$.regras")]
    [DataRow("""{"versao":1,"regras":[{}],"extra":1}""", "$.extra: campo desconhecido")]
    [DataRow("""{"versao":1,"regras":[{"id":"Cpf","categoria":"Identificacao","severidade":"Alta","chaves":["cpf"],"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".id: use minúsculas")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Financeiro","severidade":"Alta","chaves":["x"],"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".categoria: use")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"7","chaves":["x"],"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".severidade: use")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","padrao":"([a-z","mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".padrao: regex inválida")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"validador":"cnpj","mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".validador: 'cnpj' desconhecido")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"mascara":"borrar","baseLegal":"LGPD art. 5º"}]}""", ".mascara: 'borrar' desconhecida")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"mascara":"cpf","baseLegal":"art. 5º"}]}""", ".baseLegal: cite o artigo")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", "precisa de chaves, padrao ou termos")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x",""],"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".chaves: precisa ser uma lista")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"baseLegal":"LGPD art. 5º"}]}""", ".mascara: obrigatório")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"mascara":"cpf","baseLegal":"LGPD art. 5º","cor":"azul"}]}""", ".cor: campo desconhecido")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"excetoChaves":["sig"],"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".excetoChaves: só vale")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","padrao":"x","campoLivre":true,"mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".campoLivre: só vale")]
    [DataRow("""{"versao":1,"regras":[{"id":"x","categoria":"Contato","severidade":"Alta","chaves":["x"],"campoLivre":"sim","mascara":"cpf","baseLegal":"LGPD art. 5º"}]}""", ".campoLivre: precisa ser")]
    public void RecusaSchemaInvalido(string json, string trecho)
    {
        RegrasDePiiInvalidasException erro = Assert.ThrowsExactly<RegrasDePiiInvalidasException>(() => CarregadorDeRegras.Carregar(json));

        Assert.IsTrue(erro.Erros.Any(e => e.Contains(trecho, StringComparison.Ordinal)), string.Join(" | ", erro.Erros));
    }

    /// <summary>Ids repetidos derrubam a carga.</summary>
    [TestMethod]
    public void RecusaIdRepetido()
    {
        RegrasDePiiInvalidasException erro = Assert.ThrowsExactly<RegrasDePiiInvalidasException>(
            () => CarregadorDeRegras.Carregar($$"""{"versao":1,"regras":[{{RegraValida}},{{RegraValida}}]}"""));

        Assert.Contains("'matricula' repetido", erro.Message);
    }

    /// <summary>Todos os problemas aparecem juntos, para corrigir o arquivo numa passada só.</summary>
    [TestMethod]
    public void JuntaTodosOsProblemas()
    {
        RegrasDePiiInvalidasException erro = Assert.ThrowsExactly<RegrasDePiiInvalidasException>(
            () => CarregadorDeRegras.Carregar("""{"versao":3,"regras":[{"id":"x","categoria":"?","severidade":"?","chaves":["x"],"mascara":"?","baseLegal":"?"}]}"""));

        Assert.IsGreaterThanOrEqualTo(5, erro.Erros.Count);
    }
}
