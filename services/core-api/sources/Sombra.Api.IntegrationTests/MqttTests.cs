using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// A F1.9 de ponta a ponta, num Mosquitto e num PostgreSQL de verdade: a Sentinela publica assinado, o Core confere,
/// processa, empurra no hub e responde com comandos assinados (caminhos A e B do roteiro e o payload adulterado).
/// </summary>
[TestClass]
public sealed class MqttTests
{
    private const string Sentinela = "snt-7f3a9c";
    private static readonly string _segredo = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private static readonly string _tokenDoMonitor = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    /// <summary>Caminho A: a casa esquenta, o alerta abre no hub e na tela da Sentinela; o botão verde resolve.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoAPeloMqtt()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = NovaApi(relogio);
        await using OuvinteDoHub hub = await OuvinteDoHub.ConectarAsync(api, await TokenAsync(api, Demo.Operador));
        await using SentinelaDeTeste sentinela = await SentinelaDeTeste.ConectarAsync(Sentinela, _segredo, api.Prefixo);
        await api.EsperarProntoAsync();

        await sentinela.TelemetriaAsync(36, 60, seq: 1, relogio.Agora);
        JsonElement aberto = await hub.EventoAsync("AlertaAberto");
        JsonElement alertaNaTela = await sentinela.ComandoAsync("ALERTA");
        await sentinela.EventoAsync("CHECKIN_ESTOU_BEM", seq: 2, relogio.Agora);
        JsonElement resolvido = await hub.EventoAsync("AlertaResolvido");
        await sentinela.ComandoAsync("NORMAL");

        Assert.AreEqual(Demo.Lurdes.ToString(), aberto.Texto("domicilioId"));
        Assert.AreEqual("DE_SENTINELA", aberto.Texto("tipo"));
        Assert.AreEqual("PERIGO", aberto.Texto("nivel"));
        Assert.AreEqual($"{Sentinela}:1", aberto.Texto("correlationId"));
        Assert.IsGreaterThanOrEqualTo(relogio.Agora.AddMinutes(-1).ToUnixTimeSeconds(), alertaNaTela.GetProperty("seq").GetInt64());
        Assert.AreEqual(aberto.Texto("alertaId"), resolvido.Texto("alertaId"));
        Assert.AreEqual("CHECKIN_SENTINELA", resolvido.Texto("resolvidoPor"));
        Assert.AreEqual($"{Sentinela}:2", resolvido.Texto("correlationId"));
        await hub.EventoAsync("LeituraRegistrada", e => e.Texto("nivel") == "PERIGO");
        await hub.EventoAsync("CheckInRegistrado", e => e.Texto("origem") == "BOTAO_SENTINELA");
    }

    /// <summary>Caminho B: sem resposta, o relógio anda e o escalonador chama a filha e depois a vizinha; a tela avisa.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoBEscalonaPeloRelogio()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = NovaApi(relogio);
        await using OuvinteDoHub hub = await OuvinteDoHub.ConectarAsync(api, await TokenAsync(api, Demo.Ana));
        await using SentinelaDeTeste sentinela = await SentinelaDeTeste.ConectarAsync(Sentinela, _segredo, api.Prefixo);
        await api.EsperarProntoAsync();

        await sentinela.TelemetriaAsync(36, 60, seq: 1, relogio.Agora);
        await hub.EventoAsync("AlertaAberto");
        relogio.Avancar(TimeSpan.FromMinutes(15));
        JsonElement filha = await hub.EventoAsync("AlertaEscalonado", e => e.Inteiro("degrau") == 1);
        JsonElement familiaAvisada = await sentinela.ComandoAsync("FAMILIA_AVISADA");
        relogio.Avancar(TimeSpan.FromMinutes(10));
        JsonElement vizinha = await hub.EventoAsync("AlertaEscalonado", e => e.Inteiro("degrau") == 2);

        Assert.AreEqual("FAMILIAR", filha.Texto("destinatario"));
        Assert.Contains("Casa da Dona Lurdes", filha.Texto("mensagem"));
        Assert.AreEqual("VIZINHO", vizinha.Texto("destinatario"));
        Assert.AreEqual("Sua família foi avisada. Aperte o botão verde se você está bem.", familiaAvisada.Texto("mensagem"));
        Assert.IsGreaterThan(0L, await api.ConsultarAsync(db => db.Outbox.LongCountAsync(m => m.PublicadaEm != null)));
    }

    /// <summary>R10: telemetria adulterada, replay e Sentinela desconhecida viram eventos de segurança e não entram; o POST assinado também confere.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AdulteradoERepetidoViramEventoDeSeguranca()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = NovaApi(relogio);
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
        await using FabricaDaApi api = NovaApi(relogio);
        await using OuvinteDoHub hub = await OuvinteDoHub.ConectarAsync(api, await TokenAsync(api, Demo.Operador));
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

    private static FabricaDaApi NovaApi(RelogioDeTeste relogio) => new(
        configuracao: new Dictionary<string, string>
        {
            ["Demo:SentinelaDeviceId"] = Sentinela,
            ["Demo:SentinelaSegredoHex"] = _segredo,
            ["Workers:EscalonadorSegundos"] = "0.3",
            ["Workers:OutboxSegundos"] = "0.2",
            ["Ops:TokenDoMonitor"] = _tokenDoMonitor,
        },
        servicos: servicos =>
        {
            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(relogio);
        });

    private static async Task<string> TokenAsync(FabricaDaApi api, string email)
    {
        using HttpClient cliente = await api.ClienteDeAsync(email);
        return cliente.DefaultRequestHeaders.Authorization!.Parameter!;
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
