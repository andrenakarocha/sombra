using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Infraestrutura.Ia;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Infraestrutura.Tests.Apoio;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Traducao;

namespace Sombra.Infraestrutura.Tests;

/// <summary>
/// F2.6: o tradutor pega os eventos pendentes, pede à IA só com metadados, guarda a resposta aprovada, reaproveita pela
/// assinatura e cai no template sem chave, no timeout ou com a resposta reprovada.
/// </summary>
[TestClass]
public sealed class TradutorDeRiscoTests
{
    private const string Rota = "api/v1/domicilios/{id:guid}/moradores/{moradorId:guid}";

    private static readonly TraducaoDeRisco _boa = new(
        "Alguém tentou ver um dado de saúde da casa sem permissão, e o Sombra não deixou. Não precisa fazer nada.",
        "Acesso indevido a dado de saúde bloqueado (LGPD art. 11); sem incidente.",
        "Confira o token do cliente que chamou a rota.",
        TraducaoDeRisco.FonteIa);

    /// <summary>A IA traduz uma vez por assinatura: dois eventos iguais e um diferente custam duas chamadas, e o prompt não tem casa nem correlação.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task TraduzPelaIaEReaproveitaPelaAssinatura()
    {
        Func<SombraDbContext> criar = await ComEventosAsync(AcessoNegado(CategoriaDePii.Saude), AcessoNegado(CategoriaDePii.Saude), AcessoNegado(CategoriaDePii.Identificacao));
        ModeloFalso modelo = new(_ => Task.FromResult(_boa));

        await using SombraDbContext db = criar();
        int traduzidos = await Tradutor(db, modelo).TraduzirPendentesAsync(CancellationToken.None);

        Assert.AreEqual(3, traduzidos);
        Assert.AreEqual(2, modelo.Prompts.Count);
        Assert.IsTrue(modelo.Prompts.All(p => !p.Contains(Casas.Lurdes.ToString(), StringComparison.Ordinal) && !p.Contains("corr-", StringComparison.Ordinal)));
        Assert.IsTrue(await criar().EventosDeSeguranca.AllAsync(e => e.FonteDaTraducao == TraducaoDeRisco.FonteIa && e.TraducaoTecnica == _boa.Tecnico));
    }

    /// <summary>Sem chave, tudo vai para o template na hora, sem chamar ninguém.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task SemChaveUsaTemplate()
    {
        Func<SombraDbContext> criar = await ComEventosAsync(AcessoNegado(CategoriaDePii.Saude));
        ModeloFalso modelo = new(_ => Task.FromResult(_boa)) { Configurado = false };

        await using SombraDbContext db = criar();
        await Tradutor(db, modelo).TraduzirPendentesAsync(CancellationToken.None);

        EventoDeSeguranca evento = await criar().EventosDeSeguranca.SingleAsync();
        Assert.AreEqual(TraducaoDeRisco.FonteTemplate, evento.FonteDaTraducao);
        Assert.AreEqual("Alguém tentou ver um dado de saúde da casa sem permissão, e o Sombra bloqueou.", evento.TraducaoCidadao);
        Assert.IsEmpty(modelo.Prompts);
    }

    /// <summary>No timeout, aquele evento vai para o template e os outros esperam a próxima volta (o provedor está instável).</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task TimeoutCaiNoTemplateEOsOutrosEsperam()
    {
        Func<SombraDbContext> criar = await ComEventosAsync(AcessoNegado(CategoriaDePii.Saude), AcessoNegado(CategoriaDePii.Contato));
        ModeloFalso modelo = new(async token =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return _boa;
        });

        await using SombraDbContext db = criar();
        int traduzidos = await Tradutor(db, modelo, timeoutSegundos: 0.2).TraduzirPendentesAsync(CancellationToken.None);

        List<EventoDeSeguranca> eventos = await criar().EventosDeSeguranca.OrderBy(e => e.Momento).ToListAsync();
        Assert.AreEqual(1, traduzidos);
        Assert.AreEqual(TraducaoDeRisco.FonteTemplate, eventos[0].FonteDaTraducao);
        Assert.IsNull(eventos[1].TraduzidoEm);
    }

    /// <summary>Uma resposta da IA com dado pessoal ou jargão não passa na guarda: vale o template.</summary>
    /// <param name="cidadao">O que a IA respondeu para o cidadão.</param>
    /// <returns>O teste.</returns>
    [TestMethod]
    [DataRow("O Sombra bloqueou o acesso ao CPF 529.982.247-25.")]
    [DataRow("O endpoint devolveu um token, mas o log foi limpo.")]
    public async Task RespostaReprovadaViraTemplate(string cidadao)
    {
        Func<SombraDbContext> criar = await ComEventosAsync(AcessoNegado(CategoriaDePii.Saude));
        ModeloFalso modelo = new(_ => Task.FromResult(_boa with { Cidadao = cidadao }));

        await using SombraDbContext db = criar();
        await Tradutor(db, modelo).TraduzirPendentesAsync(CancellationToken.None);

        Assert.AreEqual(TraducaoDeRisco.FonteTemplate, (await criar().EventosDeSeguranca.SingleAsync()).FonteDaTraducao);
    }

    /// <summary>Cada volta faz no máximo as chamadas configuradas: o resto fica pendente, não vira template.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RespeitaAsChamadasPorVolta()
    {
        Func<SombraDbContext> criar = await ComEventosAsync(AcessoNegado(CategoriaDePii.Saude), AcessoNegado(CategoriaDePii.Contato), AcessoNegado(CategoriaDePii.Identificacao));
        ModeloFalso modelo = new(_ => Task.FromResult(_boa));

        await using SombraDbContext db = criar();
        int traduzidos = await Tradutor(db, modelo, chamadasPorVolta: 1).TraduzirPendentesAsync(CancellationToken.None);

        Assert.AreEqual(1, traduzidos);
        Assert.AreEqual(2, await criar().EventosDeSeguranca.CountAsync(e => e.TraduzidoEm == null));
    }

    /// <summary>
    /// O cliente Gemini chama <c>v1beta/models/{modelo}:generateContent</c> com a chave no header, pede JSON pelo schema
    /// e ignora as partes de raciocínio da resposta.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ClienteGeminiMontaARequisicaoELeAResposta()
    {
        string json = JsonSerializer.Serialize(new { cidadao = _boa.Cidadao, gestao = _boa.Gestao, tecnico = _boa.Tecnico });
        string resposta = JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { parts = new object[] { new { text = "pensando...", thought = true }, new { text = json } } }, finishReason = "STOP" } },
        });
        using Captura captura = new(resposta);
        using HttpClient http = new(captura, disposeHandler: false);
        ClienteGemini cliente = new(http, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GEMINI_API_KEY"] = "chave-de-teste" }).Build());

        TraducaoDeRisco traducao = await cliente.TraduzirAsync("sistema", "prompt", CancellationToken.None);

        Assert.AreEqual(_boa, traducao);
        Assert.AreEqual("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", captura.Url);
        Assert.AreEqual("chave-de-teste", captura.Chave);
        JsonElement corpo = JsonDocument.Parse(captura.Corpo!).RootElement;
        Assert.AreEqual("application/json", corpo.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
        Assert.AreEqual("object", corpo.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("type").GetString());
        Assert.AreEqual("prompt", corpo.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString());
    }

    private static TradutorDeRisco Tradutor(SombraDbContext db, ModeloFalso modelo, double timeoutSegundos = 5, int chamadasPorVolta = 5)
        => new(
            db,
            modelo,
            new RelogioFixo(Casas.Agora),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:TimeoutSegundos"] = timeoutSegundos.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Gemini:ChamadasPorVolta"] = chamadasPorVolta.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }).Build(),
            NullLogger<TradutorDeRisco>.Instance);

    private static VazamentoBloqueado AcessoNegado(CategoriaDePii categoria)
        => new(SinkDePii.RespostaHttp, Rota, "acesso-negado", categoria, SeveridadeDePii.Alta, PeloTaint: false, "corr-" + Guid.NewGuid().ToString("N")[..6], Casas.Lurdes);

    private static async Task<Func<SombraDbContext>> ComEventosAsync(params VazamentoBloqueado[] vazamentos)
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using SombraDbContext db = criar();
        int i = 0;
        db.EventosDeSeguranca.AddRange(vazamentos.Select(v => EventoDeSeguranca.DeVazamento(v with { Momento = Casas.Agora.AddMinutes(-10 + i++) }, Casas.Agora)));
        await db.SaveChangesAsync();
        return criar;
    }

    private sealed class ModeloFalso(Func<CancellationToken, Task<TraducaoDeRisco>> resposta) : IModeloDeTraducao
    {
        public List<string> Prompts { get; } = [];

        public bool Configurado { get; init; } = true;

        public string Modelo => "falso";

        public Task<TraducaoDeRisco> TraduzirAsync(string sistema, string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return resposta(cancellationToken);
        }
    }

    private sealed class Captura(string resposta) : HttpMessageHandler
    {
        public string? Url { get; private set; }

        public string? Chave { get; private set; }

        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri?.ToString();
            Chave = request.Headers.GetValues("x-goog-api-key").Single();
            Corpo = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(resposta, Encoding.UTF8, "application/json") };
        }
    }
}
