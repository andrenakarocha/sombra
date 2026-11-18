using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.Composicao;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>O log estruturado do Core (<c>docs/observabilidade.md</c>): formato, correlação e nenhum dado pessoal.</summary>
[TestClass]
public sealed partial class LogTests
{
    private static readonly string[] _camposObrigatorios = ["timestamp", "level", "service", "version", "environment", "correlationId", "event", "message"];

    /// <summary>
    /// Uma requisição com CPF, Cartão SUS e nome gera a linha <c>requisicao.concluida</c> com os oito campos e a mesma
    /// correlação devolvida no header e no ProblemDetails; nenhuma linha do log tem o dado pessoal, a senha ou o token.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RequisicaoGeraUmaLinhaJsonComCorrelacaoESemDadoPessoal()
    {
        LogCapturado log = new();
        await using FabricaDaApi api = new(log: log);
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        string cpf = DocumentosSinteticos.Cpf(91);
        string cns = DocumentosSinteticos.CartaoSus(91);
        NovoMoradorDto morador = new() { Nome = "Zuleica Quintana", DataDeNascimento = new DateOnly(1939, 7, 1), Cpf = cpf, CartaoSus = cns, FatoresDeRisco = ["CARDIOPATIA"] };

        HttpResponseMessage criado = await EnviarAsync(ana, $"/api/v1/domicilios/{Demo.Lurdes}/moradores", morador, "teste-f18:1");
        HttpResponseMessage recusado = await EnviarAsync(ana, $"/api/v1/domicilios/{Demo.Lurdes}/moradores", morador with { Cpf = "123.456.789-00" }, "teste-f18:2");
        HttpResponseMessage semHeaderValido = await EnviarAsync(ana, $"/api/v1/domicilios/{Demo.Lurdes}/moradores", morador, "não vale: tem espaço");

        Assert.AreEqual(HttpStatusCode.Created, criado.StatusCode);
        Assert.AreEqual("teste-f18:1", criado.Headers.GetValues(RastroDaRequisicao.Cabecalho).Single());
        JsonElement problema = await recusado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("teste-f18:2", problema.GetProperty("correlationId").GetString());
        Assert.AreEqual("teste-f18:2", recusado.Headers.GetValues(RastroDaRequisicao.Cabecalho).Single());
        Assert.IsTrue(Hex16().IsMatch(semHeaderValido.Headers.GetValues(RastroDaRequisicao.Cabecalho).Single()));

        JsonElement concluida = log.Eventos.Single(e => Texto(e, "event") == "requisicao.concluida" && Texto(e, "correlationId") == "teste-f18:1");
        foreach (string campo in _camposObrigatorios)
        {
            Assert.IsTrue(concluida.TryGetProperty(campo, out _), $"falta {campo}: {concluida}");
        }

        Assert.AreEqual("core-api", Texto(concluida, "service"));
        Assert.AreEqual("teste01", Texto(concluida, "version"));
        Assert.AreEqual("Test", Texto(concluida, "environment"));
        Assert.AreEqual("api/v1/domicilios/{id:guid}/moradores", Texto(concluida, "rota"));
        Assert.AreEqual(201, concluida.GetProperty("status").GetInt32());
        Assert.MatchesRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", Texto(concluida, "timestamp"));
        Assert.IsTrue(log.Eventos.Any(e => Texto(e, "event") == "requisicao.concluida" && Texto(e, "correlationId") == "teste-f18:2" && e.GetProperty("status").GetInt32() == 422));
        Assert.IsTrue(log.Eventos.Any(e => Texto(e, "event") == "servico.iniciado"));

        string tudo = string.Join('\n', log.Linhas);
        string token = ana.DefaultRequestHeaders.Authorization!.Parameter!;
        foreach (string proibido in new[] { cpf, cns, "Zuleica", "Quintana", api.SenhaDaDemo, token, Demo.Ana })
        {
            Assert.DoesNotContain(proibido, tudo, $"o log vazou: {proibido}");
        }
    }

    private static string? Texto(JsonElement evento, string campo)
        => evento.TryGetProperty(campo, out JsonElement valor) ? valor.ToString() : null;

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string url, object corpo, string correlacao)
    {
        using HttpRequestMessage requisicao = new(HttpMethod.Post, url) { Content = JsonContent.Create(corpo, options: AmbienteDaApi.Json) };
        requisicao.Headers.TryAddWithoutValidation(RastroDaRequisicao.Cabecalho, correlacao);
        return await cliente.SendAsync(requisicao);
    }

    [GeneratedRegex("^[0-9a-f]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex Hex16();
}
