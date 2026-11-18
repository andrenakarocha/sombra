using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Api.Autenticacao;
using Sombra.Api.Composicao;
using Sombra.Api.Controllers;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>A Core API de ponta a ponta: status, corpo, papéis, erros em ProblemDetails e documentação.</summary>
[TestClass]
public sealed class ApiTests
{
    private static FabricaDaApi? _api;

    private static FabricaDaApi Api => _api ?? throw new InvalidOperationException("A fábrica não subiu.");

    /// <summary>Sobe a API uma vez para a classe.</summary>
    /// <param name="contexto">O contexto do MSTest.</param>
    [ClassInitialize]
    public static void Iniciar(TestContext contexto) => _api = new FabricaDaApi();

    /// <summary>Derruba a API.</summary>
    /// <returns>A descida.</returns>
    [ClassCleanup]
    public static async Task EncerrarAsync()
    {
        if (_api is not null)
        {
            await _api.DisposeAsync();
        }
    }

    /// <summary>O login devolve o token; sem token, senha errada ou corpo inválido, a API responde com o problema certo.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LoginDevolveTokenEProtegeAsRotas()
    {
        using HttpClient anonimo = Api.CreateClient();

        TokenResposta token = await (await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", new LoginDto { Email = Demo.Ana, Senha = Api.SenhaDaDemo }))
            .CorpoAsync<TokenResposta>(HttpStatusCode.OK);
        JsonElement senhaErrada = await (await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", new LoginDto { Email = Demo.Ana, Senha = Guid.NewGuid().ToString() }))
            .ProblemaAsync(HttpStatusCode.Unauthorized);
        JsonElement invalido = await (await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", new { email = "nao-e-email" }))
            .ProblemaAsync(HttpStatusCode.BadRequest);
        await (await anonimo.GetAsync(new Uri("/api/v1/domicilios", UriKind.Relative))).ProblemaAsync(HttpStatusCode.Unauthorized);

        Assert.AreEqual("Cuidador", token.Papel);
        Assert.IsGreaterThan(DateTimeOffset.UtcNow, token.ExpiraEm);
        Assert.AreEqual("E-mail ou senha não conferem.", senhaErrada.GetProperty("detail").GetString());
        JsonElement erros = invalido.GetProperty("errors");
        Assert.IsTrue(erros.TryGetProperty("email", out _), invalido.ToString());
        Assert.IsTrue(erros.TryGetProperty("senha", out _), invalido.ToString());
    }

    /// <summary>O Cuidador só enxerga as casas dele; o painel é da operação.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CuidadorSoVeAsCasasDele()
    {
        using HttpClient ana = await Api.ClienteDeAsync(Demo.Ana);
        using HttpClient operador = await Api.ClienteDeAsync(Demo.Operador);

        Paginado<DomicilioDetalheResposta> casasDaAna = await (await ana.GetAsync(new Uri("/api/v1/domicilios", UriKind.Relative)))
            .CorpoAsync<Paginado<DomicilioDetalheResposta>>(HttpStatusCode.OK);
        Paginado<DomicilioDetalheResposta> todas = await (await operador.GetAsync(new Uri("/api/v1/domicilios", UriKind.Relative)))
            .CorpoAsync<Paginado<DomicilioDetalheResposta>>(HttpStatusCode.OK);

        Assert.IsTrue(casasDaAna.Itens.All(c => c.Id != Demo.Joaquim));
        Assert.IsTrue(casasDaAna.Itens.Any(c => c.Id == Demo.Lurdes));
        Assert.IsGreaterThanOrEqualTo(3, todas.Total);
        await (await ana.GetAsync(new Uri($"/api/v1/domicilios/{Demo.Joaquim}", UriKind.Relative))).ProblemaAsync(HttpStatusCode.Forbidden);
        await (await ana.GetAsync(new Uri("/api/v1/painel/resumo", UriKind.Relative))).ProblemaAsync(HttpStatusCode.Forbidden);
        await (await operador.EnviarAsync(HttpMethod.Post, "/api/v1/domicilios", NovaCasa())).ProblemaAsync(HttpStatusCode.Forbidden);
        PainelResumoResposta resumo = await (await operador.GetAsync(new Uri("/api/v1/painel/resumo", UriKind.Relative)))
            .CorpoAsync<PainelResumoResposta>(HttpStatusCode.OK);
        Assert.IsGreaterThanOrEqualTo(2, resumo.CasasMonitoradas);
    }

    /// <summary>O cadastro pela API: 201 com Location, PII mascarada, 404, 409 e 422 com o código da regra.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CadastroPelaApiComOsCodigosCertos()
    {
        using HttpClient ana = await Api.ClienteDeAsync(Demo.Ana);

        HttpResponseMessage criada = await ana.EnviarAsync(HttpMethod.Post, "/api/v1/domicilios", NovaCasa());
        DomicilioResposta casa = await criada.CorpoAsync<DomicilioResposta>(HttpStatusCode.Created);
        Assert.IsNotNull(criada.Headers.Location);
        await (await ana.GetAsync(criada.Headers.Location)).CorpoAsync<DomicilioDetalheResposta>(HttpStatusCode.OK);
        string casaUrl = $"/api/v1/domicilios/{casa.Id}";

        string cpf = DocumentosSinteticos.Cpf(77);
        MoradorResposta morador = await (await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/moradores", Morador(cpf)))
            .CorpoAsync<MoradorResposta>(HttpStatusCode.Created);
        Assert.AreEqual($"***.{cpf[3..6]}.***-{cpf[9..]}", morador.Cpf);
        Assert.AreEqual("R1", await RegraAsync(await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/moradores", Morador("123.456.789-00"))));

        List<ContatoResposta> rede = await (await ana.EnviarAsync(HttpMethod.Put, $"{casaUrl}/rede-de-cuidado", new RedeDeCuidadoDto
        {
            Contatos = [new ContatoDto { Nome = "Ana (filha)", Telefone = "(11) 98765-4321", Papel = PapelDoContato.Familiar }],
        })).CorpoAsync<List<ContatoResposta>>(HttpStatusCode.OK);
        Assert.AreEqual("(11) 98765-4321", rede.Single().Telefone);
        using HttpClient admin = await Api.ClienteDeAsync(Demo.Admin);
        List<ContatoResposta> vistaPeloAdmin = await (await admin.GetAsync(new Uri($"{casaUrl}/rede-de-cuidado", UriKind.Relative))).CorpoAsync<List<ContatoResposta>>(HttpStatusCode.OK);
        Assert.AreEqual("(11) *****-4321", vistaPeloAdmin.Single().Telefone);
        Assert.AreEqual("R12", await RegraAsync(await ana.EnviarAsync(HttpMethod.Put, $"{casaUrl}/rotina", new RotinaDto { CheckInDiario = new CheckInDiarioDto { Ativo = true, Horario = "23:00" }, LembreteDeAgua = new LembreteDeAguaDto { Ativo = true } })));

        await (await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/sentinelas", Sentinela("snt-7f3a9c"))).ProblemaAsync(HttpStatusCode.Conflict);
        Assert.AreEqual("VINCULO", await RegraAsync(await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/sentinelas", Sentinela("snt-abc123") with { CodigoDeVinculo = "AAAAAA" })));
        SentinelaVinculadaResposta vinculada = await (await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/sentinelas", Sentinela("snt-abc123")))
            .CorpoAsync<SentinelaVinculadaResposta>(HttpStatusCode.Created);
        Assert.MatchesRegex("^[0-9a-f]{64}$", vinculada.Segredo);
        DomicilioResposta monitorada = await (await ana.EnviarAsync(HttpMethod.Post, $"{casaUrl}/monitoramento")).CorpoAsync<DomicilioResposta>(HttpStatusCode.OK);
        Assert.IsTrue(monitorada.Monitorado);
        Assert.AreEqual("R8", await RegraAsync(await ana.EnviarAsync(HttpMethod.Delete, $"{casaUrl}/moradores/{morador.Id}")));
        await (await ana.GetAsync(new Uri($"{casaUrl}/moradores/{Guid.CreateVersion7()}", UriKind.Relative))).ProblemaAsync(HttpStatusCode.NotFound);

        DomicilioResposta renomeada = await (await ana.EnviarAsync(HttpMethod.Put, casaUrl, new AtualizacaoDeDomicilioDto
        {
            Apelido = "Casa da Vó Teresa",
            Logradouro = "Rua B",
            Numero = "2",
            Bairro = "Tucuruvi",
            Cidade = "São Paulo",
            Uf = "SP",
            Cep = "02306-000",
        })).CorpoAsync<DomicilioResposta>(HttpStatusCode.OK);
        Assert.AreEqual("Tucuruvi", renomeada.Bairro);
        await (await ana.GetAsync(new Uri("/api/v1/domicilios?nivel=CAUTELA_EXTREMA", UriKind.Relative))).CorpoAsync<Paginado<DomicilioDetalheResposta>>(HttpStatusCode.OK);
        await (await ana.GetAsync(new Uri("/api/v1/domicilios?nivel=QUENTE", UriKind.Relative))).ProblemaAsync(HttpStatusCode.BadRequest);
        Assert.AreEqual(HttpStatusCode.NoContent, (await ana.EnviarAsync(HttpMethod.Delete, casaUrl)).StatusCode);
        await (await ana.GetAsync(new Uri(casaUrl, UriKind.Relative))).ProblemaAsync(HttpStatusCode.NotFound);
    }

    /// <summary>O pedido de ajuda e a resposta da filha pela API; o Cuidador de outra casa não mexe no alerta.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PedidoDeAjudaEARespostaPelaApi()
    {
        using HttpClient ana = await Api.ClienteDeAsync(Demo.Ana);
        using HttpClient marcos = await Api.ClienteDeAsync(Demo.Marcos);

        CheckInResposta pedido = await (await ana.EnviarAsync(HttpMethod.Post, $"/api/v1/domicilios/{Demo.Lurdes}/check-ins", new NovoCheckInDto
        {
            Origem = OrigemDoCheckIn.App,
            Situacao = SituacaoDoCheckIn.PrecisoDeAjuda,
        })).CorpoAsync<CheckInResposta>(HttpStatusCode.OK);
        Assert.IsNotNull(pedido.Alerta);
        string alertaUrl = $"/api/v1/alertas/{pedido.Alerta.Id}";

        Paginado<AlertaResposta> daAna = await (await ana.GetAsync(new Uri("/api/v1/alertas?estado=ESCALONADO", UriKind.Relative)))
            .CorpoAsync<Paginado<AlertaResposta>>(HttpStatusCode.OK);
        await (await marcos.GetAsync(new Uri(alertaUrl, UriKind.Relative))).ProblemaAsync(HttpStatusCode.Forbidden);
        await (await marcos.EnviarAsync(HttpMethod.Post, $"{alertaUrl}/reconhecer", new ReconhecimentoDto { Acao = AcaoDoCuidador.EstouIndo })).ProblemaAsync(HttpStatusCode.Forbidden);
        AlertaResposta resolvido = await (await ana.EnviarAsync(HttpMethod.Post, $"{alertaUrl}/reconhecer", new ReconhecimentoDto { Acao = AcaoDoCuidador.ElaEstaBem }))
            .CorpoAsync<AlertaResposta>(HttpStatusCode.OK);
        string regra = await RegraAsync(await ana.EnviarAsync(HttpMethod.Post, $"{alertaUrl}/reconhecer", new ReconhecimentoDto { Acao = AcaoDoCuidador.VouLigar }));

        Assert.AreEqual(TipoDoAlertaNoApp.DeSentinela, pedido.Alerta.Tipo);
        Assert.AreEqual(3, pedido.Alerta.Degrau);
        Assert.IsTrue(daAna.Itens.Any(a => a.Id == pedido.Alerta.Id));
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, resolvido.Estado);
        Assert.AreEqual("R5", regra);
    }

    /// <summary>F6.25: as rotas que o app já chamava, de pé no formato dele; o Cuidador de outra casa não vê nada.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PertoDeMimEPrivacidadePelaApi()
    {
        using HttpClient ana = await Api.ClienteDeAsync(Demo.Ana);
        using HttpClient marcos = await Api.ClienteDeAsync(Demo.Marcos);
        string casaUrl = $"/api/v1/domicilios/{Demo.Lurdes}";

        List<PontoDeResfriamentoResposta> pontos = await (await ana.GetAsync(new Uri($"{casaUrl}/pontos-de-resfriamento?raioKm=3", UriKind.Relative)))
            .CorpoAsync<List<PontoDeResfriamentoResposta>>(HttpStatusCode.OK);
        PrivacidadeDaCasaResposta privacidade = await (await ana.GetAsync(new Uri($"{casaUrl}/privacidade", UriKind.Relative)))
            .CorpoAsync<PrivacidadeDaCasaResposta>(HttpStatusCode.OK);
        string corpo = await (await ana.GetAsync(new Uri($"{casaUrl}/privacidade", UriKind.Relative))).Content.ReadAsStringAsync();

        Assert.HasCount(4, pontos);
        Assert.IsTrue(pontos.Zip(pontos.Skip(1)).All(par => par.First.DistanciaKm <= par.Second.DistanciaKm));
        Assert.AreEqual(FarolDePrivacidade.Verde, privacidade.Farol);
        Assert.Contains("\"farol\":\"VERDE\"", corpo);
        await (await ana.GetAsync(new Uri($"{casaUrl}/pontos-de-resfriamento?raioKm=50", UriKind.Relative))).ProblemaAsync(HttpStatusCode.BadRequest);
        await (await marcos.GetAsync(new Uri($"{casaUrl}/privacidade", UriKind.Relative))).ProblemaAsync(HttpStatusCode.Forbidden);
        await (await marcos.GetAsync(new Uri($"{casaUrl}/pontos-de-resfriamento", UriKind.Relative))).ProblemaAsync(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// F6.23: a fila offline do app reenvia com a mesma <c>Idempotency-Key</c>. A repetição devolve a mesma resposta, sem
    /// abrir outro alerta nem virar 422 no reconhecer; a chave em outra rota é recusada, e uma chave malformada também.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task FilaOfflineNaoRepeteComAMesmaChave()
    {
        using HttpClient ana = await Api.ClienteDeAsync(Demo.Ana);
        DomicilioResposta casa = await (await ana.EnviarAsync(HttpMethod.Post, "/api/v1/domicilios", NovaCasa())).CorpoAsync<DomicilioResposta>(HttpStatusCode.Created);
        string checkInUrl = $"/api/v1/domicilios/{casa.Id}/check-ins";
        NovoCheckInDto pedido = new() { Origem = OrigemDoCheckIn.App, Situacao = SituacaoDoCheckIn.PrecisoDeAjuda };
        string chaveDoPedido = Guid.NewGuid().ToString();
        string chaveDoReconhecer = Guid.NewGuid().ToString();

        using HttpResponseMessage primeiro = await ComChaveAsync(ana, checkInUrl, pedido, chaveDoPedido);
        using HttpResponseMessage repetido = await ComChaveAsync(ana, checkInUrl, pedido, chaveDoPedido);
        CheckInResposta original = await primeiro.CorpoAsync<CheckInResposta>(HttpStatusCode.OK);
        CheckInResposta repeticao = await repetido.CorpoAsync<CheckInResposta>(HttpStatusCode.OK);
        string alertaUrl = $"/api/v1/alertas/{original.Alerta?.Id}/reconhecer";
        ReconhecimentoDto estaBem = new() { Acao = AcaoDoCuidador.ElaEstaBem };
        using HttpResponseMessage reconhecido = await ComChaveAsync(ana, alertaUrl, estaBem, chaveDoReconhecer);
        using HttpResponseMessage reenviado = await ComChaveAsync(ana, alertaUrl, estaBem, chaveDoReconhecer);
        Paginado<AlertaResposta> alertas = await (await ana.GetAsync(new Uri("/api/v1/alertas?tamanho=100", UriKind.Relative))).CorpoAsync<Paginado<AlertaResposta>>(HttpStatusCode.OK);

        Assert.AreEqual(original.Id, repeticao.Id);
        Assert.IsFalse(primeiro.Headers.Contains(FiltroDeIdempotencia.CabecalhoDeRepeticao));
        Assert.AreEqual("true", repetido.Headers.GetValues(FiltroDeIdempotencia.CabecalhoDeRepeticao).Single());
        Assert.AreEqual(1, alertas.Itens.Count(a => a.DomicilioId == casa.Id));
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, (await reconhecido.CorpoAsync<AlertaResposta>(HttpStatusCode.OK)).Estado);
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, (await reenviado.CorpoAsync<AlertaResposta>(HttpStatusCode.OK)).Estado);
        Assert.AreEqual("IDEMPOTENCIA", await RegraAsync(await ComChaveAsync(ana, alertaUrl, estaBem, chaveDoPedido)));
        await (await ComChaveAsync(ana, checkInUrl, pedido, "chave com espaço")).ProblemaAsync(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Saúde, versão, OpenAPI (todo endpoint com resumo, tag e 4xx) e a página do Scalar. O documento servido tem de ser
    /// igual ao contrato versionado em <c>docs/contratos/core-api.openapi.json</c> (<c>make openapi</c> o regenera).
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task SaudeVersaoEDocumentacao()
    {
        using HttpClient anonimo = Api.CreateClient();
        await Api.EsperarProntoAsync();

        SaudeResposta vivo = await (await anonimo.GetAsync(new Uri("/health/live", UriKind.Relative))).CorpoAsync<SaudeResposta>(HttpStatusCode.OK);
        SaudeResposta pronto = await (await anonimo.GetAsync(new Uri("/health/ready", UriKind.Relative))).CorpoAsync<SaudeResposta>(HttpStatusCode.OK);
        VersaoResposta versao = await (await anonimo.GetAsync(new Uri("/version", UriKind.Relative))).CorpoAsync<VersaoResposta>(HttpStatusCode.OK);
        string documento = await anonimo.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        using JsonDocument openApi = JsonDocument.Parse(documento);
        HttpResponseMessage scalar = await anonimo.GetAsync(new Uri("/scalar/v1", UriKind.Relative));

        Assert.AreEqual("Healthy", vivo.Status);
        Assert.AreEqual("Healthy", pronto.Verificacoes["banco"]);
        Assert.AreEqual("Healthy", pronto.Verificacoes["broker"]);
        Assert.AreEqual("teste01", versao.Commit);
        Assert.AreEqual(HttpStatusCode.OK, scalar.StatusCode);
        Assert.AreEqual("text/html", scalar.Content.Headers.ContentType?.MediaType);

        JsonElement raiz = openApi.RootElement;
        Assert.IsTrue(raiz.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        List<string> operacoes = [];
        foreach (JsonProperty caminho in raiz.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operacao in caminho.Value.EnumerateObject())
            {
                string nome = $"{operacao.Name.ToUpperInvariant()} {caminho.Name}";
                operacoes.Add(nome);
                Assert.IsFalse(string.IsNullOrWhiteSpace(operacao.Value.GetProperty("summary").GetString()), nome);
                Assert.IsGreaterThan(0, operacao.Value.GetProperty("tags").GetArrayLength(), nome);
                Assert.IsTrue(operacao.Value.GetProperty("responses").EnumerateObject().Any(r => r.Name.StartsWith('4')), nome);
            }
        }

        Assert.IsGreaterThanOrEqualTo(25, operacoes.Count, string.Join(", ", operacoes));
        await ConferirContratoAsync(documento);
    }

    private static async Task ConferirContratoAsync(string documento)
    {
        string formatado = JsonNode.Parse(documento)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        DirectoryInfo? pasta = new(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "docs", "contratos")))
        {
            pasta = pasta.Parent;
        }

        Assert.IsNotNull(pasta, "Não achei docs/contratos subindo a partir dos testes.");
        string contrato = Path.Combine(pasta.FullName, "docs", "contratos", "core-api.openapi.json");
        if (Environment.GetEnvironmentVariable("SOMBRA_ATUALIZAR_OPENAPI") == "1")
        {
            await File.WriteAllBytesAsync(contrato, Encoding.UTF8.GetBytes(formatado));
        }

        Assert.IsTrue(File.Exists(contrato), "Falta o contrato: rode make openapi.");
        Assert.AreEqual(await File.ReadAllTextAsync(contrato), formatado, "A API mudou e o contrato não: rode make openapi e revise o diff.");
    }

    private static NovoDomicilioDto NovaCasa() => new()
    {
        Apelido = "Casa de teste",
        Logradouro = "Rua A",
        Numero = "1",
        Bairro = "Santana",
        Cidade = "São Paulo",
        Uf = "SP",
        Cep = "02012-000",
        RegiaoClimatica = "SP-MIRANTE",
    };

    private static NovoMoradorDto Morador(string cpf) => new()
    {
        Nome = "Teresa T.",
        Idade = 86,
        Cpf = cpf,
        CartaoSus = DocumentosSinteticos.CartaoSus(77),
        Telefone = "(11) 92345-6789",
        FatoresDeRisco = ["CARDIOPATIA"],
    };

    private static async Task<HttpResponseMessage> ComChaveAsync(HttpClient cliente, string url, object corpo, string chave)
    {
        using HttpRequestMessage requisicao = new(HttpMethod.Post, url) { Content = JsonContent.Create(corpo, options: AmbienteDaApi.Json) };
        requisicao.Headers.Add(FiltroDeIdempotencia.Cabecalho, chave);
        return await cliente.SendAsync(requisicao);
    }

    private static NovaSentinelaDto Sentinela(string deviceId)
        => new() { DeviceId = deviceId, CodigoDeVinculo = CodigoDeVinculo.Derivar(deviceId, FabricaDaApi.ChaveDeVinculo) };

    private static async Task<string> RegraAsync(HttpResponseMessage resposta)
        => (await resposta.ProblemaAsync(HttpStatusCode.UnprocessableEntity)).GetProperty("regra").GetString() ?? string.Empty;
}

/// <summary>O limite do login, numa API só dele (o limite é por minuto e não pode vazar para os outros testes).</summary>
[TestClass]
public sealed class LimiteDoLoginTests
{
    /// <summary>Passou do limite de logins no minuto, a API responde 429 com ProblemDetails e <c>Retry-After</c>.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ExcessoDeLoginDa429()
    {
        await using FabricaDaApi api = new(loginPorMinuto: 2);
        using HttpClient anonimo = api.CreateClient();
        LoginDto errado = new() { Email = Demo.Ana, Senha = Guid.NewGuid().ToString() };

        HttpStatusCode primeiro = (await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", errado)).StatusCode;
        HttpStatusCode segundo = (await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", errado)).StatusCode;
        HttpResponseMessage terceiro = await anonimo.EnviarAsync(HttpMethod.Post, "/api/v1/auth/login", errado);

        Assert.AreEqual(HttpStatusCode.Unauthorized, primeiro);
        Assert.AreEqual(HttpStatusCode.Unauthorized, segundo);
        await terceiro.ProblemaAsync(HttpStatusCode.TooManyRequests);
        Assert.IsNotNull(terceiro.Headers.RetryAfter);
    }
}
