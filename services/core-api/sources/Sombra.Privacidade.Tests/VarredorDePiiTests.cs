using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Taint;

namespace Sombra.Privacidade.Tests;

/// <summary>A varredura do JSON de entrada: taint por chave, política de campo livre e nada de valor nos metadados.</summary>
[TestClass]
public sealed class VarredorDePiiTests
{
    private static readonly string _cpf = DocumentosSinteticos.Cpf(41);
    private static readonly string _cpfDoFilho = DocumentosSinteticos.Cpf(42);
    private static readonly string[] _camposForaDoLugar = ["$.perfilDeSaude", "$.observacoes[0]"];
    private static readonly string[] _regrasPelaChave = ["telefone", "credencial"];
    private static readonly string[] _camposDaQueryEDoHeader = ["query.senha", "header.Authorization"];

    /// <summary>
    /// Num cadastro real, o CPF no campo cpf segue marcado; o CPF do filho escrito no perfil de saúde e o telefone numa
    /// lista de observações estão fora do lugar e são mascarados, sem tocar no dado de saúde esperado.
    /// </summary>
    [TestMethod]
    public void MascaraSoODadoForaDoLugar()
    {
        JsonNode corpo = Corpo();
        ContextoDeTaint contexto = new();

        IReadOnlyList<DeteccaoDePii> foraDoLugar = new VarredorDePii(MotorDePii.Padrao, PoliticaDeCampoLivre.Mascarar).VarrerJson(corpo, contexto);

        Assert.AreEqual(_cpf, corpo["cpf"]!.GetValue<string>());
        Assert.AreEqual($"Diabetes. CPF do filho ***.{_cpfDoFilho[3..6]}.***-{_cpfDoFilho[9..]}", corpo["perfilDeSaude"]!.GetValue<string>());
        Assert.AreEqual("ligar (21) *****-4321", corpo["observacoes"]![0]!.GetValue<string>());
        Assert.AreEqual("(11) 98765-4321", corpo["contatos"]![0]!["telefone"]!.GetValue<string>());
        CollectionAssert.AreEqual(_camposForaDoLugar, foraDoLugar.Select(d => d.Campo).ToArray());
        Assert.IsTrue(foraDoLugar.All(d => d.Desfecho == DesfechoDaDeteccao.Mascarado));
        Assert.IsTrue(contexto.Contem($"log com {_cpf}") && contexto.Contem($"log com {_cpfDoFilho}") && contexto.Contem("21987654321"));
    }

    /// <summary>Na política de recusa, o JSON fica intacto e as detecções dizem campo e família, nunca o valor.</summary>
    [TestMethod]
    public void RecusaSemAlterarNada()
    {
        JsonNode corpo = Corpo();
        string antes = corpo.ToJsonString();

        IReadOnlyList<DeteccaoDePii> foraDoLugar = new VarredorDePii(MotorDePii.Padrao, PoliticaDeCampoLivre.Recusar).VarrerJson(corpo, new ContextoDeTaint());

        Assert.AreEqual(antes, corpo.ToJsonString());
        Assert.IsTrue(foraDoLugar.All(d => d.Desfecho == DesfechoDaDeteccao.Recusado));
        Assert.AreEqual(CategoriaDePii.Identificacao, foraDoLugar[0].Categoria);
        Assert.IsFalse(foraDoLugar.Any(d => d.ToString().Contains(_cpfDoFilho, System.StringComparison.Ordinal)));
    }

    /// <summary>
    /// Um campo de PII pela chave é o próprio dado: um celular que por acaso passa no dígito do CPF não vira CPF, e a
    /// senha é opaca (nunca reescrita).
    /// </summary>
    [TestMethod]
    public void CampoDePiiPelaChaveNaoEReescrito()
    {
        string celularQuePareceCpf = Enumerable.Range(1, 5000).Select(DocumentosSinteticos.Cpf).First(Validadores.Telefone);
        JsonNode corpo = JsonNode.Parse($$"""{"telefone":"{{celularQuePareceCpf}}","senha":"{{_cpf}}"}""")!;
        ContextoDeTaint contexto = new();

        IReadOnlyList<DeteccaoDePii> foraDoLugar = new VarredorDePii(MotorDePii.Padrao, PoliticaDeCampoLivre.Mascarar).VarrerJson(corpo, contexto);

        Assert.IsEmpty(foraDoLugar);
        Assert.AreEqual(celularQuePareceCpf, corpo["telefone"]!.GetValue<string>());
        CollectionAssert.AreEquivalent(_regrasPelaChave, contexto.Deteccoes.Select(d => d.RegraId).ToArray());
    }

    /// <summary>Um nome de campo com PII não vaza pelo caminho do campo.</summary>
    [TestMethod]
    public void NomeDeCampoComPiiViraMarcador()
    {
        JsonNode corpo = JsonNode.Parse($$$"""{"{{{_cpf}}}":{"obs":"ligar (11) 98765-4321"}}""")!;

        IReadOnlyList<DeteccaoDePii> foraDoLugar = new VarredorDePii(MotorDePii.Padrao, PoliticaDeCampoLivre.Mascarar).VarrerJson(corpo, new ContextoDeTaint());

        Assert.AreEqual("$.[PII].obs", foraDoLugar.Single().Campo);
    }

    /// <summary>Query string e header só marcam o taint (a senha na query de A7 fica reconhecível nos sinks).</summary>
    [TestMethod]
    public void QueryEHeaderSoMarcam()
    {
        ContextoDeTaint contexto = new();
        VarredorDePii varredor = new(MotorDePii.Padrao, PoliticaDeCampoLivre.Mascarar);

        varredor.MarcarValor("minha-senha-secreta", "senha", FonteDePii.Query, contexto);
        varredor.MarcarValor("Bearer " + CasosDePii.JwtSintetico(), "Authorization", FonteDePii.Cabecalho, contexto);

        Assert.IsTrue(contexto.Contem("GET /x?senha=minha-senha-secreta"));
        Assert.IsTrue(contexto.Contem(CasosDePii.JwtSintetico()));
        CollectionAssert.AreEqual(_camposDaQueryEDoHeader, contexto.Deteccoes.Select(d => d.Campo).ToArray());
    }

    /// <summary>O taint ignora valores curtos demais e reconhece o CPF com ou sem pontuação.</summary>
    [TestMethod]
    public void TaintReconheceDigitosEIgnoraCurtos()
    {
        ContextoDeTaint contexto = new();
        DeteccaoDePii deteccao = new(FonteDePii.Corpo, "$.x", "cpf", CategoriaDePii.Identificacao, SeveridadeDePii.Alta, OrigemDaDeteccao.Chave, DesfechoDaDeteccao.Marcado);
        string pontuado = $"{_cpf[..3]}.{_cpf[3..6]}.{_cpf[6..9]}-{_cpf[9..]}";

        contexto.Marcar(pontuado, deteccao);
        contexto.Marcar("sim", deteccao);

        Assert.IsTrue(contexto.Contem($"cpf={_cpf}"));
        Assert.IsTrue(contexto.Contem(pontuado));
        Assert.IsFalse(contexto.Contem("sim, confirmado"));
        Assert.IsFalse(contexto.Contem(null));
        Assert.AreEqual(2, contexto.ValoresMarcados);
    }

    private static JsonNode Corpo() => JsonNode.Parse($$"""
        {
          "nome": "Maria",
          "cpf": "{{_cpf}}",
          "perfilDeSaude": "Diabetes. CPF do filho {{_cpfDoFilho}}",
          "contatos": [{ "nome": "Ana", "telefone": "(11) 98765-4321" }],
          "observacoes": ["ligar 21987654321"]
        }
        """)!;
}
