using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// A conversa com a Sentinela num Mosquitto e num PostgreSQL de verdade: o que não confere (assinatura, replay, Sentinela
/// desconhecida) vira evento de segurança, e o monitor da VM chega ao painel. Os caminhos A e B estão em <see cref="RoteiroTests"/>.
/// </summary>
[TestClass]
public sealed class MqttTests
{
    private const string Sentinela = ApiComSentinela.Sentinela;
    private static readonly string _segredo = ApiComSentinela.Segredo;
    private static readonly string _tokenDoMonitor = ApiComSentinela.TokenDoMonitor;

    /// <summary>R10: telemetria adulterada, replay e Sentinela desconhecida viram eventos de segurança e não entram; o POST assinado também confere.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AdulteradoERepetidoViramEventoDeSeguranca()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = ApiComSentinela.Nova(relogio);
        await using SentinelaDeTeste sentinela = await SentinelaDeTeste.ConectarAsync(Sentinela, _segredo, api.Prefixo);
        await using SentinelaDeTeste intrusa = await SentinelaDeTeste.ConectarAsync("snt-999999", _segredo, api.Prefixo);
        await api.EsperarProntoAsync();

        await sentinela.TelemetriaAsync(30, 50, seq: 1, relogio.Agora);
        await Esperar.AteAsync(async () => await LeiturasAsync(api) == 1);
        await sentinela.TelemetriaAsync(30, 50, seq: 2, relogio.Agora, adulterar: true);
        await sentinela.TelemetriaAsync(30, 50, seq: 1, relogio.Agora);
        await intrusa.TelemetriaAsync(30, 50, seq: 1, relogio.Agora);
        await Esperar.AteAsync(async () => (await TiposDeSegurancaAsync(api)).Count == 3);

        using HttpClient http = api.CreateClient();
        HttpStatusCode valida = await PostarAsync(http, SentinelaDeTeste.MontarTelemetria(Sentinela, _segredo, 31, 50, 3, relogio.Agora));
        HttpStatusCode adulterada = await PostarAsync(http, SentinelaDeTeste.MontarTelemetria(Sentinela, _segredo, 31, 50, 4, relogio.Agora, adulterar: true));
        HttpStatusCode repetida = await PostarAsync(http, SentinelaDeTeste.MontarTelemetria(Sentinela, _segredo, 31, 50, 3, relogio.Agora));

        string[] esperados = ["MQTT_ASSINATURA_INVALIDA", "MQTT_REPLAY", "MQTT_SENTINELA_DESCONHECIDA"];
        CollectionAssert.IsSubsetOf(esperados, (await TiposDeSegurancaAsync(api)).ToArray());
        Assert.AreEqual(HttpStatusCode.Accepted, valida);
        Assert.AreEqual(HttpStatusCode.Unauthorized, adulterada);
        Assert.AreEqual(HttpStatusCode.Conflict, repetida);
        Assert.AreEqual(2, await LeiturasAsync(api));
    }

    /// <summary>O monitor da VM, com o token certo, põe um alerta de infraestrutura no hub; sem ele, 401.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AlertaDaVmChegaAoPainel()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = ApiComSentinela.Nova(relogio);
        await using OuvinteDoHub hub = await OuvinteDoHub.ConectarAsync(api, await api.TokenAsync(Demo.Operador));
        using HttpClient monitor = api.CreateClient();
        AlertaDeInfraDto alerta = new() { Host = "sombra-srv01", Recurso = "CPU", Severidade = "CRITICO", Valor = 96.4, Limite = 90, Mensagem = "CPU acima de 90%", Momento = relogio.Agora };

        HttpStatusCode semToken = (await monitor.EnviarAsync(HttpMethod.Post, "/api/v1/ops/alertas", alerta)).StatusCode;
        monitor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _tokenDoMonitor);
        HttpStatusCode comToken = (await monitor.EnviarAsync(HttpMethod.Post, "/api/v1/ops/alertas", alerta)).StatusCode;
        JsonElement evento = await hub.EventoAsync("AlertaDeInfra");

        Assert.AreEqual(HttpStatusCode.Unauthorized, semToken);
        Assert.AreEqual(HttpStatusCode.Accepted, comToken);
        Assert.AreEqual("CRITICO", evento.Texto("severidade"));
    }

    private static Task<int> LeiturasAsync(FabricaDaApi api)
        => api.ConsultarAsync(db => db.Leituras.CountAsync(l => EF.Property<Guid>(l, "DomicilioId") == Demo.Lurdes));

    private static Task<List<string>> TiposDeSegurancaAsync(FabricaDaApi api)
        => api.ConsultarAsync(db => db.EventosDeSeguranca.Select(e => e.Tipo).ToListAsync());

    private static async Task<HttpStatusCode> PostarAsync(HttpClient http, string telemetria)
    {
        using StringContent corpo = new(telemetria, Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await http.PostAsync(new Uri($"/api/v1/sentinelas/{Sentinela}/leituras", UriKind.Relative), corpo);
        return resposta.StatusCode;
    }
}
