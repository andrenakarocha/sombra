using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// Os cenários A2–A7 da auditoria (spec 07 §5) com o módulo ligado: cada endpoint de auditoria comete o erro clássico e o
/// guarda do sink correspondente bloqueia, sem o dado em nenhuma linha do log, e registra o vazamento bloqueado.
/// </summary>
[TestClass]
public sealed class GuardasDeSinkTests
{
    private const string Laudo = "Paciente com diabetes, CID E11.9, toma metformina";

    private static readonly Guid _moradoraLurdes = new("c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10");

    /// <summary>A2: a mensagem da regra violada levava o laudo; a resposta de erro sai com o laudo redigido.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A2ExcecaoComLaudoViraErroSeguro()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auditoria/excecao", new { laudo = Laudo }, AmbienteDaApi.Json);
        string corpo = await resposta.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Contains("[PII:", corpo);
        Assert.DoesNotContain("diabetes", corpo);
        Assert.DoesNotContain("E11.9", corpo);
        await EsperarVazamentoAsync(log, "Excecao", "api/v1/auditoria/excecao");
        SemONoLog(log, "diabetes", "metformina");
    }

    /// <summary>A3: o perfil de saúde sem autenticação dá 401 sem nenhum dado, e a tentativa vira evento.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A3AcessoNaoAutenticadoAoPerfilDeSaude()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient anonimo = api.CreateClient();

        using HttpResponseMessage resposta = await anonimo.GetAsync(new Uri($"/api/v1/domicilios/{Demo.Lurdes}/moradores/{_moradoraLurdes}", UriKind.Relative));
        string corpo = await resposta.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.DoesNotContain("Lurdes", corpo);
        Assert.DoesNotContain("***.", corpo);
        JsonElement evento = await EsperarVazamentoAsync(log, "RespostaHttp", "api/v1/domicilios/{id:guid}/moradores/{moradorId:guid}");
        Assert.AreEqual("acesso-negado", Texto(evento, "regra"));
        Assert.AreEqual("Saude", Texto(evento, "categoria"));
    }

    /// <summary>Resposta não autenticada que ecoa a entrada: o guarda de resposta troca por um 401 seguro, sem dado nenhum.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RespostaQueEcoaOLaudoEhBloqueada()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auditoria/eco", new { laudo = Laudo }, AmbienteDaApi.Json);
        JsonElement corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>();

        Assert.AreEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.AreEqual("Resposta bloqueada", corpo.GetProperty("title").GetString());
        Assert.IsTrue(corpo.TryGetProperty("correlationId", out _));
        Assert.DoesNotContain("diabetes", corpo.ToString());
        await EsperarVazamentoAsync(log, "RespostaHttp", "api/v1/auditoria/eco");
    }

    /// <summary>A4: o log de depuração com o Cartão SUS sai redigido e vira <c>VazamentoBloqueado{Log}</c>.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A4LogComCartaoSusEhRedigido()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();
        string cns = DocumentosSinteticos.CartaoSus(61);

        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auditoria/log", new { cartaoSus = cns }, AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.NoContent, resposta.StatusCode);
        JsonElement linha = log.Eventos.Single(e => Texto(e, "event") == "auditoria.log_de_depuracao");
        Assert.AreEqual("[PII:cns]", Texto(linha, "cartao"));
        await EsperarVazamentoAsync(log, "Log", "auditoria.log_de_depuracao");
        SemONoLog(log, cns);
    }

    /// <summary>A5: o prompt com CPF nem sai para o provedor de LLM: o handler de saída bloqueia.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A5PromptComCpfEhBloqueadoNoHandlerDeSaida()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();
        string cpf = DocumentosSinteticos.Cpf(62);

        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auditoria/llm", new { cpf }, AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.AreEqual("Saída bloqueada", (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        JsonElement evento = await EsperarAsync(log, e => Texto(e, "event") == "privacidade.saida_externa_bloqueada" && Texto(e, "onde") == "llm.auditoria.invalid");
        Assert.AreEqual("SaidaExterna", Texto(evento, "sink"));
        SemONoLog(log, cpf);
    }

    /// <summary>A6: a publicação MQTT com telefone é bloqueada no publicador.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A6PublicacaoComTelefoneEhBloqueadaNoPublicador()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();

        using HttpResponseMessage resposta = await cliente.PostAsJsonAsync("/api/v1/auditoria/mqtt", new { telefone = "(11) 97654-3210" }, AmbienteDaApi.Json);

        Assert.AreEqual(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        JsonElement evento = await EsperarAsync(log, e => Texto(e, "event") == "privacidade.saida_externa_bloqueada" && Texto(e, "sink") == "Mqtt");
        Assert.EndsWith("/auditoria/eco", Texto(evento, "onde")!);
        SemONoLog(log, "97654-3210", "97654");
    }

    /// <summary>A7: a senha na query string some do log (a linha da URL e a da requisição).</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task A7SenhaNaQueryStringSomeDoLog()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient cliente = api.CreateClient();
        string senha = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(10)); // gerada a cada execução

        using HttpResponseMessage resposta = await cliente.GetAsync(new Uri($"/api/v1/auditoria/login?usuario=ana&senha={senha}", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, resposta.StatusCode);
        JsonElement linha = log.Eventos.Single(e => Texto(e, "event") == "auditoria.login_pela_url");
        Assert.AreEqual("/api/v1/auditoria/login?usuario=ana&senha=[PII:credencial]", Texto(linha, "url"));
        await EsperarVazamentoAsync(log, "Log", "auditoria.login_pela_url");
        SemONoLog(log, senha);
    }

    private static Task<JsonElement> EsperarVazamentoAsync(LogCapturado log, string sink, string onde)
        => EsperarAsync(log, e => Texto(e, "event") == "privacidade.vazamento_bloqueado" && Texto(e, "sink") == sink && Texto(e, "onde") == onde);

    private static async Task<JsonElement> EsperarAsync(LogCapturado log, Func<JsonElement, bool> condicao)
    {
        await Esperar.AteAsync(() => log.Eventos.Any(condicao));
        return log.Eventos.First(condicao);
    }

    private static void SemONoLog(LogCapturado log, params string[] proibidos)
    {
        string tudo = string.Join('\n', log.Linhas);
        foreach (string proibido in proibidos)
        {
            Assert.DoesNotContain(proibido, tudo, $"o log vazou: {proibido}");
        }
    }

    private static string? Texto(JsonElement evento, string campo)
        => evento.TryGetProperty(campo, out JsonElement valor) ? valor.ToString() : null;
}
