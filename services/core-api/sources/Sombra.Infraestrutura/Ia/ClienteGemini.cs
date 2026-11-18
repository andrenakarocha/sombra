using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Sombra.Privacidade.Traducao;

namespace Sombra.Infraestrutura.Ia;

/// <summary>
/// A Gemini pela API REST (<c>POST /v1beta/models/{modelo}:generateContent</c>, chave no header
/// <c>x-goog-api-key</c>), com saída JSON por <c>responseJsonSchema</c> e pouco raciocínio (<c>thinkingLevel: low</c>),
/// conferidos na P5 em 2026-11-18. O <c>HttpClient</c> passa pelo guarda de saída: se o prompt tivesse PII, nem sairia.
/// </summary>
public sealed class ClienteGemini : IModeloDeTraducao
{
    /// <summary>O modelo padrão (P5: Flash-Lite estável, respondeu em 1–2 s quando os Flash maiores davam 503).</summary>
    public const string ModeloPadrao = "gemini-3.5-flash-lite";

    /// <summary>O endereço da API.</summary>
    public static readonly Uri Endereco = new("https://generativelanguage.googleapis.com/");

    private static readonly JsonObject _esquema = new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["cidadao"] = new JsonObject { ["type"] = "string" },
            ["gestao"] = new JsonObject { ["type"] = "string" },
            ["tecnico"] = new JsonObject { ["type"] = "string" },
        },
        ["required"] = new JsonArray("cidadao", "gestao", "tecnico"),
    };

    private readonly HttpClient _http;
    private readonly string? _chave;

    /// <summary>Initializes a new instance of the <see cref="ClienteGemini"/> class.</summary>
    /// <param name="http">O cliente HTTP (com o guarda de saída).</param>
    /// <param name="configuracao">A configuração: <c>GEMINI_API_KEY</c> e <c>Gemini:Modelo</c>.</param>
    public ClienteGemini(HttpClient http, IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= Endereco;
        _chave = configuracao["GEMINI_API_KEY"];
        Modelo = configuracao["Gemini:Modelo"] is { Length: > 0 } modelo ? modelo : ModeloPadrao;
    }

    /// <inheritdoc/>
    public bool Configurado => !string.IsNullOrWhiteSpace(_chave);

    /// <inheritdoc/>
    public string Modelo { get; }

    /// <inheritdoc/>
    public async Task<TraducaoDeRisco> TraduzirAsync(string sistema, string prompt, CancellationToken cancellationToken)
    {
        JsonObject corpo = new()
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = sistema }) },
            ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(new JsonObject { ["text"] = prompt }) }),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseJsonSchema"] = _esquema.DeepClone(),
                ["maxOutputTokens"] = 800,
                ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "low" },
            },
        };
        using HttpRequestMessage requisicao = new(HttpMethod.Post, new Uri($"v1beta/models/{Modelo}:generateContent", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        requisicao.Headers.Add("x-goog-api-key", _chave);
        using HttpResponseMessage resposta = await _http.SendAsync(requisicao, cancellationToken);
        resposta.EnsureSuccessStatusCode();

        JsonElement raiz = await resposta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        JsonElement candidato = raiz.GetProperty("candidates")[0];
        string texto = string.Concat(candidato.GetProperty("content").GetProperty("parts").EnumerateArray()
            .Where(p => !(p.TryGetProperty("thought", out JsonElement pensamento) && pensamento.GetBoolean()))
            .Select(p => p.TryGetProperty("text", out JsonElement t) ? t.GetString() : null));
        JsonElement traducao = JsonDocument.Parse(texto).RootElement;
        return new TraducaoDeRisco(
            traducao.GetProperty("cidadao").GetString() ?? string.Empty,
            traducao.GetProperty("gestao").GetString() ?? string.Empty,
            traducao.GetProperty("tecnico").GetString() ?? string.Empty,
            TraducaoDeRisco.FonteIa);
    }
}
