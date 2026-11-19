using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.IntegrationTests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// F2.5: os vazamentos barrados viram eventos de segurança gravados, e o farol do Risco Cidadão sai deles, no painel
/// (global, por família, por sink, por regra) e na casa (o farol que o app mostra).
/// </summary>
[TestClass]
public sealed class RiscoCidadaoApiTests
{
    private static readonly Guid _moradoraLurdes = new("c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10");

    /// <summary>
    /// Uma tentativa de ver o perfil de saúde da Dona Lurdes sem login (A3) acende o amarelo da casa dela e do painel,
    /// com a família, o sink e a regra; a trilha só tem metadados; o Cuidador não vê o painel executivo.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AcessoNegadoAcendeOFarolDaCasaEDoPainel()
    {
        await using FabricaDaApi api = new();
        using HttpClient operador = await api.ClienteDeAsync(Demo.Operador);
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        using HttpClient anonimo = api.CreateClient();
        await using OuvinteDoHub painel = await OuvinteDoHub.ConectarAsync(api, await api.TokenAsync(Demo.Operador));

        using HttpResponseMessage negado = await anonimo.GetAsync(new Uri($"/api/v1/domicilios/{Demo.Lurdes}/moradores/{_moradoraLurdes}", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.Unauthorized, negado.StatusCode);

        JsonElement eventos = default;
        await Esperar.AteAsync(async () =>
        {
            eventos = await operador.GetFromJsonAsync<JsonElement>("/api/v1/privacidade/eventos?pagina=1&tamanho=20", AmbienteDaApi.Json);
            return eventos.GetProperty("itens").EnumerateArray().Count(e => e.GetProperty("tipo").GetString() == "ACESSO_NEGADO") == 2;
        });
        JsonElement saude = eventos.GetProperty("itens").EnumerateArray().Single(e => e.GetProperty("categoria").GetString() == "SAUDE");
        Assert.AreEqual(Demo.Lurdes, saude.GetProperty("domicilioId").GetGuid());
        Assert.AreEqual("RESPOSTA_HTTP", saude.GetProperty("sink").GetString());
        Assert.AreEqual("acesso-negado", saude.GetProperty("regra").GetString());
        Assert.IsTrue(saude.GetProperty("bloqueado").GetBoolean());

        JsonElement risco = await operador.GetFromJsonAsync<JsonElement>("/api/v1/privacidade/risco-cidadao", AmbienteDaApi.Json);
        Assert.AreEqual("AMARELO", risco.GetProperty("farol").GetString());

        // o painel fica sabendo na hora, pelo hub, sem perguntar de novo
        JsonElement aoVivo = await painel.EventoAsync("RiscoCidadaoAtualizado", e => e.GetProperty("farol").GetString() == "AMARELO");
        Assert.AreEqual(JsonValueKind.Null, aoVivo.GetProperty("domicilioId").ValueKind);
        Assert.AreEqual(risco.GetProperty("pontuacao").GetDouble(), aoVivo.GetProperty("pontuacao").GetDouble(), 0.01);
        Assert.IsFalse(string.IsNullOrWhiteSpace(aoVivo.GetProperty("mensagemCidadao").GetString()));
        Assert.AreEqual(2, risco.GetProperty("eventosNaJanela").GetInt32());
        Assert.AreEqual("SAUDE", risco.GetProperty("porCategoria")[0].GetProperty("categoria").GetString());
        Assert.AreEqual("RespostaHttp", risco.GetProperty("porSink")[0].GetProperty("nome").GetString());
        Assert.AreEqual("acesso-negado", risco.GetProperty("topRegras")[0].GetProperty("nome").GetString());

        JsonElement casa = await ana.GetFromJsonAsync<JsonElement>($"/api/v1/domicilios/{Demo.Lurdes}/privacidade", AmbienteDaApi.Json);
        Assert.AreEqual("AMARELO", casa.GetProperty("farol").GetString());
        Assert.IsTrue(casa.GetProperty("mensagens").EnumerateArray().Any(m => m.GetProperty("mensagemCidadao").GetString() == "Alguém tentou ver um dado de saúde da casa sem permissão, e o Sombra bloqueou."));

        JsonElement outraCasa = await operador.GetFromJsonAsync<JsonElement>($"/api/v1/domicilios/{Demo.Joaquim}/privacidade", AmbienteDaApi.Json);
        Assert.AreEqual("VERDE", outraCasa.GetProperty("farol").GetString());

        using HttpResponseMessage cuidador = await ana.GetAsync(new Uri("/api/v1/privacidade/risco-cidadao", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.Forbidden, cuidador.StatusCode);
    }
}
