using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Calor;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// O roteiro da demonstração, "a onda de calor da Dona Lurdes" (<c>docs/PLANO.md</c> §3.4), de ponta a ponta: Sentinela
/// por MQTT assinado, Core com PostgreSQL e Mosquitto reais, app e painel pelo hub e pela API.
/// </summary>
[TestClass]
public sealed class RoteiroTests
{
    /// <summary>Passos 1 a 4: a casa no verde, a previsão de perigo, a casa esquenta e o botão verde; o painel mede o tempo até o check-in.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoA()
    {
        RelogioDeTeste relogio = new();
        ClimaDeTeste clima = new();
        await using FabricaDaApi api = ApiComSentinela.Nova(relogio, clima);
        using HttpClient filha = await api.ClienteDeAsync(Demo.Ana);
        using HttpClient operador = await api.ClienteDeAsync(Demo.Operador);
        await using OuvinteDoHub app = await OuvinteDoHub.ConectarAsync(api, await api.TokenAsync(Demo.Ana));
        await using SentinelaDeTeste sentinela = await SentinelaDeTeste.ConectarAsync(ApiComSentinela.Sentinela, ApiComSentinela.Segredo, api.Prefixo);
        await api.EsperarProntoAsync();

        // 1. A casa no verde, Sentinela online.
        await sentinela.TelemetriaAsync(24, 55, seq: 1, relogio.Agora);
        await app.EventoAsync("LeituraRegistrada");
        DomicilioDetalheResposta noVerde = await (await filha.GetAsync(new Uri($"/api/v1/domicilios/{Demo.Lurdes}", UriKind.Relative))).CorpoAsync<DomicilioDetalheResposta>(HttpStatusCode.OK);

        // 2. O Clima prevê perigo em 24 h: alerta preventivo, a tela pede para encher a garrafa, a filha é avisada.
        clima.Previsto = NivelDeRisco.Perigo;
        JsonElement preventivo = await app.EventoAsync("AlertaAberto", e => e.Texto("tipo") == "PREVENTIVO");
        JsonElement garrafa = await sentinela.ComandoAsync("PRE_ALERTA");
        JsonElement avisoDaPrevisao = await app.EventoAsync("AvisoParaARede", e => e.Texto("motivo") == "PREVISAO");

        // 3. A casa esquenta: a Sentinela pede o "estou bem" e o app fica vermelho ao vivo.
        await sentinela.TelemetriaAsync(36, 60, seq: 2, relogio.Agora);
        JsonElement vermelho = await app.EventoAsync("AlertaAberto", e => e.Texto("tipo") == "DE_SENTINELA");
        await sentinela.ComandoAsync("ALERTA");

        // 4. Caminho A: seis minutos depois, ela aperta o botão; o painel mostra o tempo até o check-in.
        relogio.Avancar(TimeSpan.FromMinutes(6));
        await sentinela.EventoAsync("CHECKIN_ESTOU_BEM", seq: 3, DateTimeOffset.UtcNow);
        JsonElement resolvido = await app.EventoAsync("AlertaResolvido");
        await sentinela.ComandoAsync("NORMAL");
        PainelResumoResposta painel = await (await operador.GetAsync(new Uri("/api/v1/painel/resumo", UriKind.Relative))).CorpoAsync<PainelResumoResposta>(HttpStatusCode.OK);

        Assert.AreEqual(NivelDeRisco.Normal, noVerde.NivelAtual);
        Assert.IsTrue(noVerde.Sentinela?.Online);
        Assert.AreEqual(Demo.Lurdes.ToString(), preventivo.Texto("domicilioId"));
        Assert.AreEqual("Amanhã: calor perigoso. Encha sua garrafa de água.", garrafa.Texto("mensagem"));
        Assert.AreEqual("FAMILIAR", avisoDaPrevisao.Texto("destinatario"));
        Assert.AreEqual("PERIGO", vermelho.Texto("nivel"));
        Assert.AreEqual(vermelho.Texto("alertaId"), resolvido.Texto("alertaId"));
        Assert.AreEqual("CHECKIN_SENTINELA", resolvido.Texto("resolvidoPor"));
        Assert.IsNotNull(painel.TempoMedianoAteCheckInMin);
        Assert.IsTrue(painel.TempoMedianoAteCheckInMin is >= 6.0 and < 7.0, $"tempo até o check-in: {painel.TempoMedianoAteCheckInMin}");
        Assert.AreEqual(100.0, painel.AlertasResolvidosSemUltimoDegrauPct);
    }

    /// <summary>Passo 5, caminho B (D13): sem resposta, a filha, a vizinha e só então a rede; a filha confirma que está tudo bem.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoB()
    {
        RelogioDeTeste relogio = new();
        await using FabricaDaApi api = ApiComSentinela.Nova(relogio);
        using HttpClient filha = await api.ClienteDeAsync(Demo.Ana);
        using HttpClient operador = await api.ClienteDeAsync(Demo.Operador);
        await using OuvinteDoHub app = await OuvinteDoHub.ConectarAsync(api, await api.TokenAsync(Demo.Ana));
        await using SentinelaDeTeste sentinela = await SentinelaDeTeste.ConectarAsync(ApiComSentinela.Sentinela, ApiComSentinela.Segredo, api.Prefixo);
        await api.EsperarProntoAsync();

        await sentinela.TelemetriaAsync(36, 60, seq: 1, relogio.Agora);
        JsonElement alerta = await app.EventoAsync("AlertaAberto");
        relogio.Avancar(TimeSpan.FromMinutes(15));
        JsonElement lembreteLeve = await app.EventoAsync("AlertaEscalonado", e => e.Inteiro("degrau") == 1);
        JsonElement familiaAvisada = await sentinela.ComandoAsync("FAMILIA_AVISADA");
        relogio.Avancar(TimeSpan.FromMinutes(10));
        JsonElement quemEstaPerto = await app.EventoAsync("AlertaEscalonado", e => e.Inteiro("degrau") == 2);
        relogio.Avancar(TimeSpan.FromMinutes(10));
        JsonElement redeToda = await app.EventoAsync("AlertaEscalonado", e => e.Inteiro("degrau") == 3);
        AlertaResposta bem = await (await filha.EnviarAsync(HttpMethod.Post, $"/api/v1/alertas/{alerta.Texto("alertaId")}/reconhecer", new ReconhecimentoDto { Acao = Dominio.Alertas.AcaoDoCuidador.ElaEstaBem }))
            .CorpoAsync<AlertaResposta>(HttpStatusCode.OK);
        JsonElement resolvido = await app.EventoAsync("AlertaResolvido");
        await sentinela.ComandoAsync("NORMAL");
        PainelResumoResposta painel = await (await operador.GetAsync(new Uri("/api/v1/painel/resumo", UriKind.Relative))).CorpoAsync<PainelResumoResposta>(HttpStatusCode.OK);

        Assert.AreEqual("FAMILIAR", lembreteLeve.Texto("destinatario"));
        Assert.Contains("Quer ligar?", lembreteLeve.Texto("mensagem"));
        Assert.AreEqual("Sua família foi avisada. Aperte o botão verde se você está bem.", familiaAvisada.Texto("mensagem"));
        Assert.AreEqual("VIZINHO", quemEstaPerto.Texto("destinatario"));
        Assert.Contains("Pode dar uma olhada?", quemEstaPerto.Texto("mensagem"));
        Assert.AreEqual("REDE", redeToda.Texto("destinatario"));
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, bem.Estado);
        Assert.AreEqual("CUIDADOR", resolvido.Texto("resolvidoPor"));
        Assert.AreEqual(1, painel.AlarmesDesnecessariosSemana);
        Assert.AreEqual(0.0, painel.AlertasResolvidosSemUltimoDegrauPct);
        Assert.AreEqual(1, app.Recebidos.Count(r => r.Nome == "AlertaEscalonado" && r.Dados.Inteiro("degrau") == 3));
    }
}
