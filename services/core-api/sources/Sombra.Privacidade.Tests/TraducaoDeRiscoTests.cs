using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Traducao;

namespace Sombra.Privacidade.Tests;

/// <summary>A tradução de risco (spec 07 §3): o prompt nunca tem PII (P5), os templates passam na guarda e a guarda barra o que a IA errar.</summary>
[TestClass]
public sealed class TraducaoDeRiscoTests
{
    private static readonly string[] _tipos =
    [
        "ACESSO_NEGADO", "VAZAMENTO_BLOQUEADO", "SAIDA_EXTERNA_BLOQUEADA", "PII_FORA_DO_LUGAR",
        "MQTT_ASSINATURA_INVALIDA", "MQTT_REPLAY", "MQTT_FORA_DA_JANELA", "MQTT_MENSAGEM_INVALIDA", "VAZAMENTO_PERMITIDO", "OUTRO_TIPO",
    ];

    private static readonly string[] _camposDoPrompt = ["tipo", "sink", "categoria", "severidade", "regra", "onde", "desfecho", "repeticoesEm24h"];

    private static readonly string _cpf = DocumentosSinteticos.Cpf(81);

    // O "onde" vem de rota-modelo, evento de log, host e tópico; os adversários simulam um bug que pusesse o dado ali.
    private static readonly string[] _ondes =
    [
        "api/v1/domicilios/{id:guid}/moradores/{moradorId:guid}",
        "auditoria.log_de_depuracao",
        "llm.auditoria.invalid",
        "sombra/v1/snt-7f3a9c/comando",
        $"api/v1/moradores?cpf={_cpf}",
        "ligar para (11) 98765-4321",
        "maria@exemplo.com",
        _cpf,
    ];

    /// <summary>
    /// P5: em todas as combinações de tipo, sink, família, severidade, desfecho e "onde" (inclusive adversários), o prompt
    /// não tem nenhum dado pessoal para o motor, não carrega o "onde" adversário e só tem os campos da lista fechada.
    /// </summary>
    [TestMethod]
    public void PromptNuncaTemPii()
    {
        GuardaDeSaida guarda = new(MotorDePii.Padrao);
        Assert.IsTrue(guarda.Verificar(PromptDeRisco.Sistema, null).Limpo, "a instrução de sistema tem PII");
        int conferidos = 0;
        foreach (EventoParaTraduzir evento in Todos())
        {
            string prompt = PromptDeRisco.Montar(evento);

            Assert.IsTrue(guarda.Verificar(prompt, null).Limpo, $"prompt com PII: {evento.Tipo}/{evento.Sink}/{evento.Categoria}");
            Assert.IsFalse(_ondes[4..].Any(o => prompt.Contains(o, StringComparison.Ordinal) || prompt.Contains(_cpf, StringComparison.Ordinal)), "o onde adversário entrou no prompt");
            JsonElement metadados = JsonDocument.Parse(prompt[prompt.IndexOf('{', StringComparison.Ordinal)..]).RootElement;
            CollectionAssert.AreEqual(_camposDoPrompt, metadados.EnumerateObject().Select(p => p.Name).ToArray());
            conferidos++;
        }

        Assert.IsGreaterThan(10_000, conferidos);
    }

    /// <summary>Todo template, em toda combinação, passa na mesma guarda que a resposta da IA.</summary>
    [TestMethod]
    public void TemplatesPassamNaGuarda()
    {
        foreach (EventoParaTraduzir evento in Todos())
        {
            TraducaoDeRisco template = TemplatesDeRisco.Para(evento);

            Assert.IsTrue(GuardaDaTraducao.Aprovar(template, out string motivo), $"{evento.Tipo}/{evento.Sink}/{evento.Categoria}: {motivo}");
            Assert.AreEqual(TraducaoDeRisco.FonteTemplate, template.Fonte);
        }
    }

    /// <summary>A guarda barra PII, jargão, mais de duas frases, texto longo, campo vazio e palavra sem acento (as duas
    /// últimas linhas são traduções reais do Gemini que passaram antes desta regra); aprova uma tradução boa.</summary>
    /// <param name="cidadao">A mensagem do cidadão.</param>
    /// <param name="motivo">O motivo esperado (vazio quando aprova).</param>
    [TestMethod]
    [DataRow("Alguém tentou ver um dado de saúde da casa e o Sombra bloqueou. Não precisa fazer nada.", "")]
    [DataRow("O Sombra bloqueou o CPF 529.982.247-25 da casa.", "dado_pessoal")]
    [DataRow("Ligue para (11) 98765-4321 se tiver dúvida.", "dado_pessoal")]
    [DataRow("Um token da casa foi bloqueado no log.", "jargao_para_o_cidadao")]
    [DataRow("O Sombra bloqueou. Está tudo bem. Não precisa fazer nada.", "cidadao_com_mais_de_duas_frases")]
    [DataRow(" ", "campo_vazio")]
    [DataRow("Tentaram acessar seus dados de identificacao sem autorizacao, mas o sistema bloqueou a tentativa.", "sem_acento")]
    [DataRow("Fique tranquilo, suas informacoes estao seguras e ninguem precisa fazer nada.", "sem_acento")]
    public void GuardaDaIa(string cidadao, string motivo)
    {
        bool aprovada = GuardaDaTraducao.Aprovar(new TraducaoDeRisco(cidadao, "Bloqueado (LGPD art. 11).", "Use Sensivel<T> no log.", TraducaoDeRisco.FonteIa), out string obtido);

        Assert.AreEqual(motivo, obtido);
        Assert.AreEqual(motivo.Length == 0, aprovada);
    }

    /// <summary>A guarda também barra a linha da gestão longa demais ou sem acento e PII na sugestão técnica, e não cobra
    /// acento dos identificadores que a sugestão técnica cita (o id da regra, uma chave de configuração).</summary>
    [TestMethod]
    public void GuardaOlhaAsTresAudiencias()
    {
        Assert.IsFalse(GuardaDaTraducao.Aprovar(new TraducaoDeRisco("Tudo certo.", new string('a', GuardaDaTraducao.MaximoGestao + 1), "ok", "IA"), out string longa));
        Assert.AreEqual("longa_demais", longa);
        Assert.IsFalse(GuardaDaTraducao.Aprovar(new TraducaoDeRisco("Tudo certo.", "ok", $"O campo cpf={_cpf} vazou.", "IA"), out string pii));
        Assert.AreEqual("dado_pessoal", pii);
        Assert.IsFalse(GuardaDaTraducao.Aprovar(new TraducaoDeRisco("Tudo certo.", "Risco a seguranca dos dados (LGPD art. 46).", "ok", "IA"), out string gestao));
        Assert.AreEqual("sem_acento", gestao);
        Assert.IsTrue(GuardaDaTraducao.Aprovar(new TraducaoDeRisco("Tudo certo.", "ok", "A regra saude disparou: defina Privacidade__Aplicacao=Ligada e use Sensivel<T>.", "IA"), out _));
    }

    /// <summary>A assinatura junta eventos iguais (sem contar as repetições) e separa os diferentes.</summary>
    [TestMethod]
    public void AssinaturaJuntaEventosIguais()
    {
        EventoParaTraduzir um = new("ACESSO_NEGADO", SinkDePii.RespostaHttp, CategoriaDePii.Saude, SeveridadeDePii.Alta, "acesso-negado", "api/v1/x", true, 1);

        Assert.AreEqual(um.Assinatura, (um with { Repeticoes = 7 }).Assinatura);
        Assert.AreNotEqual(um.Assinatura, (um with { Onde = "api/v1/y" }).Assinatura);
        Assert.AreNotEqual(um.Assinatura, (um with { Bloqueado = false }).Assinatura);
        Assert.AreEqual(64, um.Assinatura.Length);
    }

    private static IEnumerable<EventoParaTraduzir> Todos()
    {
        SinkDePii?[] sinks = [null, .. Enum.GetValues<SinkDePii>().Cast<SinkDePii?>()];
        foreach (string tipo in _tipos)
        {
            foreach (SinkDePii? sink in sinks)
            {
                foreach (CategoriaDePii categoria in Enum.GetValues<CategoriaDePii>())
                {
                    foreach (SeveridadeDePii severidade in Enum.GetValues<SeveridadeDePii>())
                    {
                        foreach (string onde in _ondes)
                        {
                            yield return new EventoParaTraduzir(tipo, sink, categoria, severidade, tipo == "ACESSO_NEGADO" ? "acesso-negado" : "cpf", onde, Bloqueado: true, Repeticoes: 2);
                            yield return new EventoParaTraduzir(tipo, sink, categoria, severidade, "saude", onde, Bloqueado: false);
                        }
                    }
                }
            }
        }
    }
}
