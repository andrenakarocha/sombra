using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet;
using MQTTnet.Protocol;
using Sombra.Api.Controllers;
using Sombra.Api.IntegrationTests.Apoio;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Api.IntegrationTests;

/// <summary>
/// A auditoria antes × depois (spec 07 §5, F2.8): os oito cenários rodam com o módulo desligado (o modo inseguro, só no
/// ambiente Demo) e ligado. Desligado, cada um vaza e a trilha registra o vazamento; ligado, cada um é bloqueado. Com
/// <c>SOMBRA_RELATORIO_AUDITORIA</c> (o <c>make auditoria</c>), grava <c>relatorio.md</c> e <c>relatorio.json</c> nessa
/// pasta, com a requisição, a resposta, o log e os eventos de cada lado. Só dados sintéticos.
/// </summary>
[TestClass]
public sealed partial class AuditoriaAntesEDepoisTests
{
    private const string CpfDaLurdes = "34875763689";

    private static readonly Guid _moradoraLurdes = new("c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10");
    private static readonly string _cpfDoFilho = DocumentosSinteticos.Cpf(301);
    private static readonly string _cartao = DocumentosSinteticos.CartaoSus(301);
    private static readonly string _cpfNoPrompt = DocumentosSinteticos.Cpf(302);
    private const string Laudo = "Paciente com diabetes, CID E11.9, toma metformina";
    private const string Telefone = "(11) 97654-3210";
    private const string SenhaRedigida = "‹senha sintética›";

    // Gerada a cada execução e redigida no relatório: o "antes" do A7 prova o vazamento sem gravar senha em arquivo.
    private static readonly string _senhaNaUrl = Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(10));

    private static readonly JsonSerializerOptions _relatorioJson = new(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly (string Id, string Nome, string Esperado, Func<Contexto, Task<Saida>> Executar)[] _cenarios =
    [
        ("A1", "POST de morador com CPF dentro do texto livre de saúde", "mascarado antes de gravar e logar", A1Async),
        ("A2", "POST que provoca exceção com o laudo no payload", "resposta de erro segura, sem o laudo", A2Async),
        ("A3", "GET não autenticado do perfil de saúde", "401 sem nenhum dado, evento registrado", A3Async),
        ("A4", "Endpoint de depuração que loga o Cartão SUS", "log redigido e VazamentoBloqueado{Log}", A4Async),
        ("A5", "PII no prompt da LLM", "bloqueada no handler de saída", A5Async),
        ("A6", "PII publicada no MQTT", "bloqueada no publicador", A6Async),
        ("A7", "Senha na query string", "removida do log", A7Async),
        ("A8", "Telemetria MQTT com HMAC falso", "descartada, evento de segurança", A8Async),
    ];

    /// <summary>Os oito cenários vazam com o módulo desligado e são bloqueados com ele ligado (🔎 da F2.8).</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task OitoCenariosVazamDesligadoESaoBloqueadosLigado()
    {
        Dictionary<string, Execucao> desligado = await RodarAsync(ligado: false);
        Dictionary<string, Execucao> ligado = await RodarAsync(ligado: true);
        Cenario[] cenarios = [.. _cenarios.Select(c => new Cenario(c.Id, c.Nome, c.Esperado, desligado[c.Id], ligado[c.Id]))];

        if (Environment.GetEnvironmentVariable("SOMBRA_RELATORIO_AUDITORIA") is { Length: > 0 } pasta)
        {
            Directory.CreateDirectory(pasta);
            Cenario[] redigidos = [.. cenarios.Select(c => c with { Desligado = SemSenha(c.Desligado), Ligado = SemSenha(c.Ligado) })];
            await using (FileStream json = File.Create(Path.Combine(pasta, "relatorio.json")))
            {
                await JsonSerializer.SerializeAsync(json, redigidos, _relatorioJson);
            }

            await using StreamWriter md = new(Path.Combine(pasta, "relatorio.md"));
            await md.WriteAsync(Markdown(redigidos));
        }

        foreach (Cenario c in cenarios)
        {
            Assert.IsTrue(c.Desligado.Vazou, $"{c.Id} não vazou com o módulo desligado: {c.Desligado.Evidencia}");
            Assert.IsTrue(c.Desligado.Eventos.Count > 0 && c.Desligado.Eventos.All(e => e.EndsWith("vazou", StringComparison.Ordinal)), $"{c.Id} desligado sem evento de vazamento");
            Assert.IsFalse(c.Ligado.Vazou, $"{c.Id} vazou com o módulo ligado: {c.Ligado.Evidencia}");
            Assert.IsTrue(c.Ligado.Eventos.Count > 0 && c.Ligado.Eventos.All(e => e.EndsWith("bloqueado", StringComparison.Ordinal)), $"{c.Id} ligado sem evento de bloqueio");
        }
    }

    private static async Task<Dictionary<string, Execucao>> RodarAsync(bool ligado)
    {
        string modo = ligado ? "ligado" : "desligado";
        LogCapturado log = new();
        await using FabricaDaApi api = new(
            log: log,
            ambiente: "Demo",
            configuracao: new Dictionary<string, string>
            {
                ["Privacidade:Aplicacao"] = ligado ? "Ligada" : "Desligada",
                ["Auditoria:UrlDoLlm"] = "http://localhost/api/v1/auditoria/receptor/",
                ["Demo:SentinelaDeviceId"] = ApiComSentinela.Sentinela,
                ["Demo:SentinelaSegredoHex"] = ApiComSentinela.Segredo,
            },
            servicos: servicos => servicos.Configure<HttpClientFactoryOptions>(AuditoriaController.ClienteLlm, opcoes =>
                opcoes.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = ((TestServer)b.Services.GetRequiredService<IServer>()).CreateHandler())));
        using HttpClient anonimo = api.CreateClient();
        using HttpClient ana = await api.ClienteDeAsync(Demo.Ana);
        await using OuvinteMqtt ouvinte = await OuvinteMqtt.ConectarAsync($"{api.Prefixo}/auditoria/eco");
        await api.EsperarProntoAsync();

        Dictionary<string, (string Correlacao, Saida Saida)> saidas = [];
        foreach ((string id, _, _, Func<Contexto, Task<Saida>> executar) in _cenarios)
        {
            string correlacao = $"auditoria-{id}-{modo}";
            Saida saida = await executar(new Contexto(api, anonimo, ana, ouvinte, log, correlacao, ligado));
            saidas[id] = (saida.Correlacao ?? correlacao, saida);
        }

        List<(string? Correlacao, string Linha)> eventos = [];
        string[] faltando = [];
        for (DateTime limite = DateTime.UtcNow.AddSeconds(15); DateTime.UtcNow < limite; await Task.Delay(200))
        {
            eventos = await api.ConsultarAsync(async db => (await db.EventosDeSeguranca.AsNoTracking().ToListAsync())
                .Select(e => (e.CorrelationId, $"{e.Tipo} · {e.Sink?.ToString() ?? "MQTT"} · {e.Categoria?.ToString() ?? "Credencial"} · regra {e.Regra ?? "hmac"} · {(e.Bloqueado ? "bloqueado" : "vazou")}"))
                .ToList());
            faltando = [.. saidas.Where(s => !eventos.Any(e => e.Correlacao == s.Value.Correlacao)).Select(s => s.Key)];
            if (faltando.Length == 0)
            {
                break;
            }
        }

        Assert.IsEmpty(faltando, $"{modo}: cenários sem evento de segurança: {string.Join(", ", faltando)}; correlações na trilha: {string.Join(", ", eventos.Select(e => e.Correlacao ?? "(nula)").Distinct())}");

        return saidas.ToDictionary(
            s => s.Key,
            s => new Execucao(
                modo,
                s.Value.Saida.Requisicao,
                s.Value.Saida.Status,
                s.Value.Saida.Resposta,
                [.. log.Eventos.Where(e => Texto(e, "correlationId") == s.Value.Correlacao && Texto(e, "event") is not ("requisicao.concluida" or "privacidade.pii_detectada"))
                    .Select(e => Trecho($"{Texto(e, "event")}: {Texto(e, "message")}", 240))],
                [.. eventos.Where(e => e.Correlacao == s.Value.Correlacao).Select(e => e.Linha).Distinct().Order(StringComparer.Ordinal)],
                s.Value.Saida.Vazou,
                s.Value.Saida.Evidencia));
    }

    private static async Task<Saida> A1Async(Contexto c)
    {
        object corpo = new
        {
            nome = "Iracema Duarte",
            idade = 86,
            cpf = DocumentosSinteticos.Cpf(303),
            cartaoSus = DocumentosSinteticos.CartaoSus(303),
            perfilDeSaude = $"Hipertensão. Em caso de urgência, CPF do filho {_cpfDoFilho}",
            fatoresDeRisco = Array.Empty<string>(),
        };
        (int status, string resposta) = await EnviarAsync(c.Ana, HttpMethod.Post, $"/api/v1/domicilios/{Demo.Lurdes}/moradores", corpo, c.Correlacao);
        Guid id = JsonDocument.Parse(resposta).RootElement.GetProperty("id").GetGuid();
        string gravado = await c.Api.ConsultarAsync(async db => (await db.Domicilios.AsNoTracking().SingleAsync(d => d.Id == Demo.Lurdes)).Moradores.Single(m => m.Id == id).ObservacoesDeSaude ?? string.Empty);
        return new Saida($"POST /api/v1/domicilios/{{Lurdes}}/moradores {Json(corpo)}", status, resposta, gravado.Contains(_cpfDoFilho, StringComparison.Ordinal), $"texto livre gravado no banco (decifrado): {gravado}");
    }

    private static async Task<Saida> A2Async(Contexto c)
    {
        object corpo = new { laudo = Laudo };
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Post, "/api/v1/auditoria/excecao", corpo, c.Correlacao);
        return new Saida($"POST /api/v1/auditoria/excecao {Json(corpo)}", status, resposta, resposta.Contains("diabetes", StringComparison.Ordinal), "o laudo aparece na mensagem de erro?");
    }

    private static async Task<Saida> A3Async(Contexto c)
    {
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Get, $"/api/v1/auditoria/perfil/{Demo.Lurdes}/{_moradoraLurdes}", null, c.Correlacao);
        return new Saida("GET /api/v1/auditoria/perfil/{Lurdes}/{moradora} (sem login)", status, resposta, resposta.Contains(CpfDaLurdes, StringComparison.Ordinal), "o CPF da Dona Lurdes aparece na resposta?");
    }

    private static async Task<Saida> A4Async(Contexto c)
    {
        object corpo = new { cartaoSus = _cartao };
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Post, "/api/v1/auditoria/log", corpo, c.Correlacao);
        string linha = c.Log.Eventos.Where(e => Texto(e, "event") == "auditoria.log_de_depuracao" && Texto(e, "correlationId") == c.Correlacao).Select(e => Texto(e, "message")).Single() ?? string.Empty;
        return new Saida($"POST /api/v1/auditoria/log {Json(corpo)}", status, resposta, string.Join('\n', c.Log.Linhas).Contains(_cartao, StringComparison.Ordinal), $"linha de log: {linha}");
    }

    private static async Task<Saida> A5Async(Contexto c)
    {
        object corpo = new { cpf = _cpfNoPrompt };
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Post, "/api/v1/auditoria/llm", corpo, c.Correlacao);
        return new Saida($"POST /api/v1/auditoria/llm {Json(corpo)}", status, resposta, resposta.Contains(_cpfNoPrompt, StringComparison.Ordinal), "o provedor de LLM (eco) recebeu o CPF no prompt?");
    }

    private static async Task<Saida> A6Async(Contexto c)
    {
        object corpo = new { telefone = Telefone };
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Post, "/api/v1/auditoria/mqtt", corpo, c.Correlacao);
        if (c.Ligado)
        {
            await Task.Delay(TimeSpan.FromSeconds(1.5));
        }
        else
        {
            await Esperar.AteAsync(() => !c.Ouvinte.Mensagens.IsEmpty, 10);
        }

        string recebida = c.Ouvinte.Mensagens.LastOrDefault() ?? "(nada publicado)";
        return new Saida($"POST /api/v1/auditoria/mqtt {Json(corpo)}", status, resposta, recebida.Contains("97654-3210", StringComparison.Ordinal), $"mensagem no broker: {recebida}");
    }

    private static async Task<Saida> A7Async(Contexto c)
    {
        (int status, string resposta) = await EnviarAsync(c.Anonimo, HttpMethod.Get, $"/api/v1/auditoria/login?usuario=ana&senha={_senhaNaUrl}", null, c.Correlacao);
        string linha = c.Log.Eventos.Where(e => Texto(e, "event") == "auditoria.login_pela_url" && Texto(e, "correlationId") == c.Correlacao).Select(e => Texto(e, "message")).Single() ?? string.Empty;
        return new Saida("GET /api/v1/auditoria/login?usuario=ana&senha=…", status, resposta, string.Join('\n', c.Log.Linhas).Contains(_senhaNaUrl, StringComparison.Ordinal), $"linha de log: {linha}");
    }

    private static async Task<Saida> A8Async(Contexto c)
    {
        int antes = await LeiturasAsync(c.Api);
        string telemetria = SentinelaDeTeste.MontarTelemetria(ApiComSentinela.Sentinela, ApiComSentinela.Segredo, 31, 50, 1, DateTimeOffset.UtcNow, adulterar: true);
        using HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri($"/api/v1/sentinelas/{ApiComSentinela.Sentinela}/leituras", UriKind.Relative))
        {
            Content = new StringContent(telemetria, Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add("X-Correlation-Id", c.Correlacao);
        using HttpResponseMessage resposta = await c.Anonimo.SendAsync(requisicao);
        int depois = await LeiturasAsync(c.Api);
        // A mensagem da Sentinela é correlacionada por deviceId:seq (docs/observabilidade.md §3), não pelo header.
        return new Saida($"POST /api/v1/sentinelas/{ApiComSentinela.Sentinela}/leituras (temperatura alterada depois de assinar)", (int)resposta.StatusCode, await resposta.Content.ReadAsStringAsync(), depois > antes, $"leituras da casa: {antes} → {depois}", $"{ApiComSentinela.Sentinela}:1");
    }

    private static Task<int> LeiturasAsync(FabricaDaApi api)
        => api.ConsultarAsync(db => db.Leituras.CountAsync(l => EF.Property<Guid>(l, "DomicilioId") == Demo.Lurdes));

    private static async Task<(int Status, string Corpo)> EnviarAsync(HttpClient cliente, HttpMethod metodo, string url, object? corpo, string correlacao)
    {
        using HttpRequestMessage requisicao = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            requisicao.Content = new StringContent(JsonSerializer.Serialize(corpo, AmbienteDaApi.Json), Encoding.UTF8, "application/json");
        }

        requisicao.Headers.Add("X-Correlation-Id", correlacao);
        using HttpResponseMessage resposta = await cliente.SendAsync(requisicao);
        return ((int)resposta.StatusCode, TraceId().Replace(await resposta.Content.ReadAsStringAsync(), string.Empty));
    }

    private static string Markdown(Cenario[] cenarios)
    {
        StringBuilder md = new();
        md.AppendLine("# Auditoria do protótipo: antes × depois do Sombra.Privacidade")
            .AppendLine()
            .AppendLine("> Gerado por `security/auditoria/rodar-auditoria.sh` (teste `OitoCenariosVazamDesligadoESaoBloqueadosLigado`, spec 07 §5).")
            .AppendLine("> \"Antes\" é o módulo desligado (`Privacidade:Aplicacao=Desligada`, aceito só no ambiente `Demo`): os guardas detectam e")
            .AppendLine("> registram, mas deixam passar. \"Depois\" é o módulo ligado. Todos os dados são sintéticos (CPF e Cartão SUS gerados por semente).")
            .AppendLine()
            .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"**{cenarios.Count(c => c.Desligado.Vazou)} de {cenarios.Length} cenários vazam antes · {cenarios.Count(c => !c.Ligado.Vazou)} de {cenarios.Length} são bloqueados depois**")
            .AppendLine()
            .AppendLine("| # | Cenário | Esperado com o módulo | Antes (desligado) | Depois (ligado) |")
            .AppendLine("|---|---|---|---|---|");
        foreach (Cenario c in cenarios)
        {
            md.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"| {c.Id} | {c.Nome} | {c.Esperado} | {c.Desligado.Status} · {(c.Desligado.Vazou ? "❌ vazou" : "✅ não vazou")} | {c.Ligado.Status} · {(c.Ligado.Vazou ? "❌ vazou" : "✅ bloqueado")} |");
        }

        foreach (Cenario c in cenarios)
        {
            md.AppendLine().AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"## {c.Id} · {c.Nome}").AppendLine()
                .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"Requisição: `{c.Ligado.Requisicao}`");
            foreach (Execucao e in new[] { c.Desligado, c.Ligado })
            {
                md.AppendLine().AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"**{(e.Modo == "ligado" ? "Depois (ligado)" : "Antes (desligado)")}** · HTTP {e.Status} · {(e.Vazou ? "vazou" : "não vazou")}").AppendLine()
                    .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Evidência: {Celula(e.Evidencia)}")
                    .AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Resposta: `{Celula(Trecho(e.Resposta, 300))}`");
                foreach (string linha in e.Log)
                {
                    md.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Log: `{Celula(linha)}`");
                }

                foreach (string evento in e.Eventos)
                {
                    md.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"- Evento de segurança: {evento}");
                }
            }
        }

        return md.ToString();
    }

    private static Execucao SemSenha(Execucao e) => e with
    {
        Resposta = e.Resposta.Replace(_senhaNaUrl, SenhaRedigida, StringComparison.Ordinal),
        Evidencia = e.Evidencia.Replace(_senhaNaUrl, SenhaRedigida, StringComparison.Ordinal),
        Log = [.. e.Log.Select(l => l.Replace(_senhaNaUrl, SenhaRedigida, StringComparison.Ordinal))],
    };

    private static string Json(object corpo) => JsonSerializer.Serialize(corpo, AmbienteDaApi.Json);

    private static string Trecho(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo] + "…";

    private static string Celula(string texto) => texto.Replace("`", "'", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string? Texto(JsonElement evento, string campo)
        => evento.TryGetProperty(campo, out JsonElement valor) ? valor.ToString() : null;

    [GeneratedRegex("\"traceId\":\"[^\"]*\",?", RegexOptions.CultureInvariant)]
    private static partial Regex TraceId();

    internal sealed class OuvinteMqtt : IAsyncDisposable
    {
        private readonly IMqttClient _cliente = new MqttClientFactory().CreateMqttClient();

        internal ConcurrentQueue<string> Mensagens { get; } = new();

        internal static async Task<OuvinteMqtt> ConectarAsync(string topico)
        {
            OuvinteMqtt ouvinte = new();
            ouvinte._cliente.ApplicationMessageReceivedAsync += argumentos =>
            {
                ouvinte.Mensagens.Enqueue(argumentos.ApplicationMessage.ConvertPayloadToString());
                return Task.CompletedTask;
            };
            await ouvinte._cliente.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(AmbienteDaApi.MqttHost, AmbienteDaApi.MqttPorta).WithClientId($"auditoria-{Guid.NewGuid():N}"[..22]).Build());
            await ouvinte._cliente.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter(topico, MqttQualityOfServiceLevel.AtLeastOnce).Build());
            return ouvinte;
        }

        public async ValueTask DisposeAsync()
        {
            await _cliente.DisconnectAsync();
            _cliente.Dispose();
        }
    }
}

/// <summary>O que um cenário recebe para rodar.</summary>
/// <param name="Api">A API.</param>
/// <param name="Anonimo">Um cliente sem login.</param>
/// <param name="Ana">O cliente da Ana, Cuidadora da Dona Lurdes.</param>
/// <param name="Ouvinte">Quem escuta o tópico do A6 no broker.</param>
/// <param name="Log">O log capturado.</param>
/// <param name="Correlacao">A correlação do cenário neste modo.</param>
/// <param name="Ligado">Se o módulo está ligado.</param>
internal sealed record Contexto(FabricaDaApi Api, HttpClient Anonimo, HttpClient Ana, AuditoriaAntesEDepoisTests.OuvinteMqtt Ouvinte, LogCapturado Log, string Correlacao, bool Ligado);

/// <summary>O que um cenário devolve.</summary>
/// <param name="Requisicao">A requisição, como vai no relatório.</param>
/// <param name="Status">O status HTTP.</param>
/// <param name="Resposta">O corpo da resposta.</param>
/// <param name="Vazou">Se o dado chegou ao sink.</param>
/// <param name="Evidencia">O que prova o vazamento ou o bloqueio.</param>
/// <param name="Correlacao">A correlação dos eventos, quando não é a do header (a Sentinela usa <c>deviceId:seq</c>).</param>
internal sealed record Saida(string Requisicao, int Status, string Resposta, bool Vazou, string Evidencia, string? Correlacao = null);

/// <summary>Um cenário num modo, com o log e os eventos de segurança dele.</summary>
/// <param name="Modo"><c>desligado</c> ou <c>ligado</c>.</param>
/// <param name="Requisicao">A requisição.</param>
/// <param name="Status">O status HTTP.</param>
/// <param name="Resposta">O corpo da resposta.</param>
/// <param name="Log">As linhas de log da correlação do cenário.</param>
/// <param name="Eventos">Os eventos de segurança da correlação do cenário.</param>
/// <param name="Vazou">Se o dado chegou ao sink.</param>
/// <param name="Evidencia">O que prova.</param>
internal sealed record Execucao(string Modo, string Requisicao, int Status, string Resposta, IReadOnlyList<string> Log, IReadOnlyList<string> Eventos, bool Vazou, string Evidencia);

/// <summary>Um cenário da auditoria, antes e depois.</summary>
/// <param name="Id">A1 a A8.</param>
/// <param name="Nome">O cenário.</param>
/// <param name="Esperado">O esperado com o módulo (spec 07 §5).</param>
/// <param name="Desligado">O antes.</param>
/// <param name="Ligado">O depois.</param>
internal sealed record Cenario(string Id, string Nome, string Esperado, Execucao Desligado, Execucao Ligado);
