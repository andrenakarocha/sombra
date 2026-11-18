using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// F6.26, o contrato vivo entre o Core e o app (D17): cada rota que o app usa responde no formato das amostras de
/// <c>docs/contratos/amostras/</c>, e o Jest do app passa as mesmas amostras pelos schemas Zod dele. O Core mudou um
/// campo? Este teste fica vermelho; regravou as amostras (<c>make -C services/core-api amostras</c>)? O do app fica.
/// </summary>
[TestClass]
public sealed class ContratoDoAppTests
{
    private static readonly JsonSerializerOptions _gravacao = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>As respostas reais têm os mesmos campos e tipos das amostras versionadas.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AmostrasDoAppBatemComOCore()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = ApiComSentinela.Nova(relogio);
        using HttpClient http = api.CreateClient();
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        string casa = $"/api/v1/domicilios/{Demo.Lurdes}";
        Dictionary<string, JsonNode> amostras = new(StringComparer.Ordinal);

        amostras["login"] = Sem(await CorpoAsync(await http.PostAsJsonAsync("/api/v1/auth/login", new LoginDto { Email = Demo.Ana, Senha = api.SenhaDaDemo }, AmbienteDaApi.Json)), "token", "<jwt>");
        Assert.AreEqual(HttpStatusCode.Accepted, await TelemetriaAsync(http, SentinelaDeTeste.MontarTelemetria(ApiComSentinela.Sentinela, ApiComSentinela.Segredo, 36, 60, 1, relogio.Agora)));
        await TelemetriaAsync(http, SentinelaDeTeste.MontarTelemetria(ApiComSentinela.Sentinela, ApiComSentinela.Segredo, 36, 60, 2, relogio.Agora, adulterar: true));
        string periodo = $"de={Uri.EscapeDataString(relogio.Agora.AddHours(-1).ToString("O"))}&ate={Uri.EscapeDataString(relogio.Agora.AddMinutes(1).ToString("O"))}";

        amostras["domicilios"] = await GetAsync(ana, "/api/v1/domicilios?tamanho=100");
        amostras["domicilio"] = await GetAsync(ana, casa);
        amostras["leituras"] = await GetAsync(ana, $"{casa}/leituras?{periodo}&tamanho=100");
        amostras["moradores"] = await GetAsync(ana, $"{casa}/moradores");
        amostras["rede-de-cuidado"] = await GetAsync(ana, $"{casa}/rede-de-cuidado");
        amostras["alertas"] = await GetAsync(ana, "/api/v1/alertas?tamanho=100");
        string alerta = amostras["domicilio"]["alertaAberto"]!["id"]!.GetValue<string>();
        amostras["alerta"] = await GetAsync(ana, $"/api/v1/alertas/{alerta}");
        amostras["rotina"] = await GetAsync(ana, $"{casa}/rotina");
        amostras["pontos-de-resfriamento"] = await GetAsync(ana, $"{casa}/pontos-de-resfriamento?raioKm=3");
        amostras["privacidade"] = await GetAsync(ana, $"{casa}/privacidade");
        amostras["rotina-salva"] = await EnviarAsync(ana, HttpMethod.Put, $"{casa}/rotina", new RotinaDto
        {
            CheckInDiario = new CheckInDiarioDto { Ativo = false, Horario = "10:30" },
            LembreteDeAgua = new LembreteDeAguaDto { Ativo = true },
        });
        amostras["alerta-reconhecido"] = await EnviarAsync(ana, HttpMethod.Post, $"/api/v1/alertas/{alerta}/reconhecer", new ReconhecimentoDto { Acao = AcaoDoCuidador.VouLigar });
        amostras["check-in"] = await EnviarAsync(ana, HttpMethod.Post, $"{casa}/check-ins", new NovoCheckInDto
        {
            Origem = OrigemDoCheckIn.App,
            Situacao = SituacaoDoCheckIn.EstouBem,
            Momento = relogio.Agora,
        });
        amostras["morador-criado"] = await EnviarAsync(ana, HttpMethod.Post, $"{casa}/moradores", new NovoMoradorDto
        {
            Nome = "Moradora de teste",
            Idade = 81,
            Cpf = DocumentosSinteticos.Cpf(61),
            CartaoSus = DocumentosSinteticos.CartaoSus(61),
            Telefone = "(11) 93456-7812",
            PerfilDeSaude = "hipertensão",
            FatoresDeRisco = ["CARDIOPATIA"],
        });
        string morador = amostras["morador-criado"]["id"]!.GetValue<string>();
        amostras["morador-editado"] = await EnviarAsync(ana, HttpMethod.Put, $"{casa}/moradores/{morador}", new AtualizacaoDeMoradorDto
        {
            Nome = "Moradora de teste",
            Idade = 82,
            FatoresDeRisco = ["CARDIOPATIA", "USO_DE_DIURETICO"],
        });
        JsonNode nova = await EnviarAsync(ana, HttpMethod.Post, "/api/v1/domicilios", new NovoDomicilioDto
        {
            Apelido = "Casa de teste",
            Logradouro = "Rua de Teste",
            Numero = "10",
            Bairro = "Santana",
            Cidade = "São Paulo",
            Uf = "SP",
            Cep = "02012-000",
            RegiaoClimatica = "SP-MIRANTE",
        });
        amostras["sentinela-vinculada"] = Sem(
            await EnviarAsync(ana, HttpMethod.Post, $"/api/v1/domicilios/{nova["id"]}/sentinelas", new NovaSentinelaDto
            {
                DeviceId = "snt-abc123",
                CodigoDeVinculo = CodigoDeVinculo.Derivar("snt-abc123", FabricaDaApi.ChaveDeVinculo),
            }),
            "segredo",
            new string('0', 64));

        await ConferirAsync(amostras);
    }

    private static async Task ConferirAsync(Dictionary<string, JsonNode> amostras)
    {
        DirectoryInfo? pasta = new(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "docs", "contratos")))
        {
            pasta = pasta.Parent;
        }

        Assert.IsNotNull(pasta, "Não achei docs/contratos subindo a partir dos testes.");
        string destino = Path.Combine(pasta.FullName, "docs", "contratos", "amostras");
        if (Environment.GetEnvironmentVariable("SOMBRA_ATUALIZAR_AMOSTRAS") == "1")
        {
            Directory.CreateDirectory(destino);
            foreach ((string nome, JsonNode corpo) in amostras)
            {
                await File.WriteAllTextAsync(Path.Combine(destino, $"{nome}.json"), corpo.ToJsonString(_gravacao) + "\n", new UTF8Encoding(false));
            }

            return;
        }

        string[] gravadas = Directory.Exists(destino) ? [.. Directory.GetFiles(destino, "*.json").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal)!] : [];
        CollectionAssert.AreEqual(amostras.Keys.Order(StringComparer.Ordinal).ToArray(), gravadas, "As amostras do contrato mudaram: rode make -C services/core-api amostras e o Jest do app.");
        foreach ((string nome, JsonNode corpo) in amostras)
        {
            JsonNode gravada = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(destino, $"{nome}.json")))!;
            string? diferenca = Diferenca(corpo, gravada, nome);
            Assert.IsNull(diferenca, $"A resposta de {nome} não tem mais o formato da amostra ({diferenca}): rode make -C services/core-api amostras e o Jest do app.");
        }
    }

    // Compara o formato, não os valores (IDs e horários mudam a cada execução): os mesmos campos, com o mesmo tipo; nulo
    // combina com qualquer tipo, e nas listas vale o primeiro item.
    private static string? Diferenca(JsonNode? viva, JsonNode? gravada, string caminho)
    {
        if (viva is null || gravada is null)
        {
            return null;
        }

        if (Tipo(viva) != Tipo(gravada))
        {
            return $"{caminho}: {Tipo(viva)} em vez de {Tipo(gravada)}";
        }

        if (viva is JsonObject objeto && gravada is JsonObject outro)
        {
            string[] campos = [.. objeto.Select(c => c.Key).Order(StringComparer.Ordinal)];
            string[] esperados = [.. outro.Select(c => c.Key).Order(StringComparer.Ordinal)];
            return !campos.SequenceEqual(esperados, StringComparer.Ordinal)
                ? $"{caminho}: campos [{string.Join(", ", campos)}] em vez de [{string.Join(", ", esperados)}]"
                : campos.Select(c => Diferenca(objeto[c], outro[c], $"{caminho}.{c}")).FirstOrDefault(d => d is not null);
        }

        return viva is JsonArray lista && gravada is JsonArray outra && lista.Count > 0 && outra.Count > 0
            ? Diferenca(lista[0], outra[0], $"{caminho}[0]")
            : null;
    }

    private static string Tipo(JsonNode no) => no switch
    {
        JsonObject => "objeto",
        JsonArray => "lista",
        _ => no.GetValueKind() switch
        {
            JsonValueKind.String => "texto",
            JsonValueKind.Number => "número",
            JsonValueKind.True or JsonValueKind.False => "booleano",
            JsonValueKind kind => kind.ToString(),
        },
    };

    private static JsonNode Sem(JsonNode corpo, string campo, string substituto)
    {
        corpo[campo] = substituto;
        return corpo;
    }

    private static async Task<JsonNode> GetAsync(HttpClient cliente, string url) => await CorpoAsync(await cliente.GetAsync(new Uri(url, UriKind.Relative)));

    private static async Task<JsonNode> EnviarAsync(HttpClient cliente, HttpMethod metodo, string url, object corpo) => await CorpoAsync(await cliente.EnviarAsync(metodo, url, corpo));

    private static async Task<JsonNode> CorpoAsync(HttpResponseMessage resposta)
    {
        using (resposta)
        {
            string texto = await resposta.Content.ReadAsStringAsync();
            Assert.IsTrue(resposta.IsSuccessStatusCode, $"{resposta.RequestMessage?.RequestUri}: {(int)resposta.StatusCode} {texto}");
            return JsonNode.Parse(texto)!;
        }
    }

    private static async Task<HttpStatusCode> TelemetriaAsync(HttpClient http, string telemetria)
    {
        using StringContent corpo = new(telemetria, Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await http.PostAsync(new Uri($"/api/v1/sentinelas/{ApiComSentinela.Sentinela}/leituras", UriKind.Relative), corpo);
        return resposta.StatusCode;
    }
}
