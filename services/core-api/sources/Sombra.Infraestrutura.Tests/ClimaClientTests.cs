using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Geo;
using Sombra.Infraestrutura.Clima;

namespace Sombra.Infraestrutura.Tests;

/// <summary>
/// F3.9: o Core lê o Clima pelo contrato. As respostas são as amostras reais que o próprio Clima grava e confere
/// (<c>docs/contratos/amostras-clima/</c>); a resiliência é o circuito com o último risco conhecido.
/// </summary>
[TestClass]
public sealed class ClimaClientTests
{
    private static readonly PontoGeografico _mirante = PontoGeografico.Criar(-23.4964, -46.6200);

    /// <summary>O risco do pico real de nov/2023, como o Clima responde: agora e previsão em CAUTELA_EXTREMA, pela regra.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LeORiscoDaAmostraRealDoClima()
    {
        using Cenario c = new(_ => Responder(HttpStatusCode.OK, Amostra("risco-calor.json")));

        RiscoDaRegiao? risco = await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None);

        Assert.IsNotNull(risco);
        Assert.AreEqual(NivelDeRisco.CautelaExtrema, risco.NivelAgora);
        Assert.AreEqual(NivelDeRisco.CautelaExtrema, risco.NivelPrevisto);
        Assert.AreEqual(0.07, risco.ProbabilidadePerigo, 1e-9);
        Assert.AreEqual("regra", risco.Fonte);
        Assert.AreEqual("/api/v1/regioes/SP-MIRANTE/risco-calor", c.Pedidos.Single().RequestUri!.AbsolutePath);
    }

    /// <summary>Os pontos perto da estação, da amostra real (OpenStreetMap), no formato do app.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LeOsPontosDaAmostraRealDoClima()
    {
        using Cenario c = new(_ => Responder(HttpStatusCode.OK, Amostra("pontos-proximos.json")));

        IReadOnlyList<PontoDeResfriamentoResposta> pontos = await c.Cliente.ProximosAsync(_mirante, 3, CancellationToken.None);

        Assert.AreEqual(8, pontos.Count);
        PontoDeResfriamentoResposta primeiro = pontos[0];
        Assert.AreEqual("Biblioteca Municipal Nuto Sant'anna", primeiro.Nome);
        Assert.AreEqual("BIBLIOTECA", primeiro.Tipo);
        Assert.AreEqual(0.2, primeiro.DistanciaKm, 1e-9);
        Assert.AreEqual(new HorarioDeFuncionamentoResposta("09:00", "18:00"), primeiro.Horario);
        Assert.AreEqual(-23.498444, primeiro.Lat, 1e-9);
        Assert.StartsWith("OpenStreetMap", primeiro.Fonte);
        Assert.IsTrue(pontos.Zip(pontos.Skip(1)).All(p => p.First.DistanciaKm <= p.Second.DistanciaKm));
        Assert.AreEqual("lat=-23.4964&lon=-46.62&raioKm=3", c.Pedidos.Single().RequestUri!.Query.TrimStart('?'));
    }

    /// <summary>404 é resposta (o Clima não conhece a região): nulo, sem contar como falha nem servir um risco antigo.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RegiaoDesconhecidaENulaSemAbrirOCircuito()
    {
        using Cenario c = new(_ => Responder(HttpStatusCode.NotFound, "{}"));

        for (int i = 0; i < EstadoDoClima.FalhasParaAbrir + 1; i++)
        {
            Assert.IsNull(await c.Cliente.ObterRiscoAsync("SP-XX", CancellationToken.None));
        }

        Assert.IsFalse(c.Estado.Aberto);
        Assert.HasCount(EstadoDoClima.FalhasParaAbrir + 1, c.Pedidos);
    }

    /// <summary>
    /// Depois de três falhas seguidas o circuito abre e nenhuma chamada sai; enquanto isso vale o último risco conhecido
    /// (até 30 min). Passados 30 s, uma chamada de teste passa e, com sucesso, o circuito fecha.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CircuitoAbreServeOUltimoRiscoEFechaQuandoOClimaVolta()
    {
        bool fora = false;
        using Cenario c = new(_ => fora ? Responder(HttpStatusCode.InternalServerError, "{}") : Responder(HttpStatusCode.OK, Amostra("risco-calor.json")));
        RiscoDaRegiao? conhecido = await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None);

        fora = true;
        for (int i = 0; i < EstadoDoClima.FalhasParaAbrir; i++)
        {
            Assert.AreEqual(conhecido, await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        }

        Assert.IsTrue(c.Estado.Aberto);
        int pedidos = c.Pedidos.Count;
        Assert.AreEqual(conhecido, await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        Assert.HasCount(pedidos, c.Pedidos, "com o circuito aberto, nada sai");

        c.Tempo.Agora += EstadoDoClima.ValidadeDoUltimoRisco + TimeSpan.FromMinutes(1);
        Assert.IsNull(await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None), "risco velho não vale como agora");
        Assert.IsTrue(c.Estado.Aberto, "a chamada de teste falhou: o circuito reabre");

        fora = false;
        c.Tempo.Agora += EstadoDoClima.TempoAberto + TimeSpan.FromSeconds(1);
        Assert.IsNotNull(await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        Assert.IsFalse(c.Estado.Aberto);
    }

    /// <summary>Timeout, rede fora e corpo com nível desconhecido viram nulo, sem lançar para quem chama.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task FalhasNaoLancamParaQuemChama()
    {
        using Cenario lento = new(_ => throw new TaskCanceledException("timeout"));
        using Cenario fora = new(_ => throw new HttpRequestException("conexão recusada"));
        using Cenario estranho = new(_ => Responder(HttpStatusCode.OK, Amostra("risco-calor.json").Replace("CAUTELA_EXTREMA", "MORNO", StringComparison.Ordinal)));

        Assert.IsNull(await lento.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        Assert.IsNull(await fora.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        Assert.IsNull(await estranho.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None));
        Assert.IsEmpty(await fora.Cliente.ProximosAsync(_mirante, 3, CancellationToken.None));
    }

    /// <summary>O correlationId do trabalho atual vai no header: a chamada aparece nos logs do Core e do Clima com o mesmo ID.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LevaOCorrelationIdParaOClima()
    {
        using Cenario c = new(_ => Responder(HttpStatusCode.OK, Amostra("risco-calor.json")));
        Correlacao.Atual = "snt-7f3a9c:57";
        try
        {
            await c.Cliente.ObterRiscoAsync("SP-MIRANTE", CancellationToken.None);
        }
        finally
        {
            Correlacao.Atual = null;
        }

        Assert.AreEqual("snt-7f3a9c:57", c.Pedidos.Single().Headers.GetValues(CorrelacaoNaSaida.Header).Single());
    }

    private static string Amostra(string nome)
    {
        DirectoryInfo? pasta = new(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "docs", "contratos", "amostras-clima")))
        {
            pasta = pasta.Parent;
        }

        Assert.IsNotNull(pasta, "Não achei docs/contratos/amostras-clima subindo a partir dos testes.");
        return File.ReadAllText(Path.Combine(pasta.FullName, "docs", "contratos", "amostras-clima", nome), Encoding.UTF8);
    }

    private static HttpResponseMessage Responder(HttpStatusCode status, string corpo) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    /// <summary>Um ClimaClient com a correlação de saída, um servidor falso e um relógio controlado.</summary>
    private sealed class Cenario : IDisposable
    {
        private readonly ServidorFalso _servidor;
        private readonly CorrelacaoNaSaida _correlacao;
        private readonly HttpClient _http;

        public Cenario(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            Estado = new EstadoDoClima(Tempo);
            _servidor = new ServidorFalso(responder, Pedidos);
            _correlacao = new CorrelacaoNaSaida { InnerHandler = _servidor };
            _http = new HttpClient(_correlacao, disposeHandler: false) { BaseAddress = new Uri("http://clima.teste/") };
            Cliente = new ClimaClient(_http, Estado, NullLogger<ClimaClient>.Instance);
        }

        public TempoDeTeste Tempo { get; } = new();

        public EstadoDoClima Estado { get; }

        public ClimaClient Cliente { get; }

        public List<HttpRequestMessage> Pedidos { get; } = [];

        public void Dispose()
        {
            _http.Dispose();
            _correlacao.Dispose();
            _servidor.Dispose();
        }
    }

    private sealed class ServidorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder, List<HttpRequestMessage> pedidos) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            pedidos.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class TempoDeTeste : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
