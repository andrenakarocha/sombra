using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Sombra.Api.Composicao;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Api.Privacidade;

/// <summary>
/// A porta de entrada do taint (spec 07 §1): lê o JSON de toda requisição (com buffering), marca no
/// <see cref="ContextoDeTaint"/> o que é PII pela chave e pelo conteúdo, e aplica a política de campo livre: mascara o
/// dado fora do lugar antes do model binding (então ele nunca chega ao banco nem ao log) ou recusa com 422. Query string
/// e <c>Authorization</c> só são marcados. Cada detecção vira o evento <c>privacidade.pii_detectada</c>, sem o valor.
/// </summary>
internal sealed class PiiScanningMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly VarredorDePii _varredor;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ILogger<PiiScanningMiddleware> _log;

    /// <summary>Initializes a new instance of the <see cref="PiiScanningMiddleware"/> class.</summary>
    /// <param name="proximo">O próximo passo do pipeline.</param>
    /// <param name="varredor">O varredor, com a política configurada.</param>
    /// <param name="observador">Quem registra o dado fora do lugar como evento de segurança.</param>
    /// <param name="log">O log.</param>
    public PiiScanningMiddleware(RequestDelegate proximo, VarredorDePii varredor, IObservadorDeVazamentos observador, ILogger<PiiScanningMiddleware> log)
    {
        _proximo = proximo;
        _varredor = varredor;
        _observador = observador;
        _log = log;
    }

    /// <summary>Varre a requisição e segue, mascarada, ou responde 422.</summary>
    /// <param name="http">A requisição.</param>
    /// <param name="contexto">O taint desta requisição.</param>
    /// <returns>O processamento.</returns>
    public async Task InvokeAsync(HttpContext http, ContextoDeTaint contexto)
    {
        foreach ((string chave, StringValues valores) in http.Request.Query)
        {
            foreach (string? valor in valores)
            {
                _varredor.MarcarValor(valor ?? string.Empty, chave, FonteDePii.Query, contexto);
            }
        }

        if (http.Request.Headers.Authorization.ToString() is { Length: > 0 } autorizacao)
        {
            _varredor.MarcarValor(autorizacao, HeaderNames.Authorization, FonteDePii.Cabecalho, contexto);
        }

        IReadOnlyList<DeteccaoDePii> foraDoLugar = TemCorpoJson(http.Request) ? await VarrerCorpoAsync(http, contexto) : [];
        foreach (DeteccaoDePii d in contexto.Deteccoes)
        {
            _log.PiiDetectada(d.Desfecho == DesfechoDaDeteccao.Marcado ? LogLevel.Debug : LogLevel.Warning, d.Categoria, d.Campo, d.Fonte, d.RegraId, d.Origem, d.Desfecho);
        }

        string rota = (http.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
        foreach (DeteccaoDePii d in foraDoLugar)
        {
            _observador.Registrar(new VazamentoBloqueado(SinkDePii.Entrada, rota, d.RegraId, d.Categoria, d.Severidade, PeloTaint: false, http.TraceIdentifier, Bloqueado: d.Desfecho != DesfechoDaDeteccao.Registrado));
        }

        if (foraDoLugar.Any(d => d.Desfecho == DesfechoDaDeteccao.Recusado))
        {
            await RecusarAsync(http, foraDoLugar);
            return;
        }

        await _proximo(http);
    }

    private static bool TemCorpoJson(HttpRequest requisicao)
        => (HttpMethods.IsPost(requisicao.Method) || HttpMethods.IsPut(requisicao.Method) || HttpMethods.IsPatch(requisicao.Method))
            && requisicao.ContentLength is null or > 0
            && MediaTypeHeaderValue.TryParse(requisicao.ContentType, out MediaTypeHeaderValue? tipo)
            && (tipo.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                || tipo.Suffix.Equals("json", StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<DeteccaoDePii>> VarrerCorpoAsync(HttpContext http, ContextoDeTaint contexto)
    {
        http.Request.EnableBuffering();
        JsonNode? raiz;
        try
        {
            raiz = await JsonNode.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
        }
        catch (JsonException)
        {
            raiz = null; // JSON malformado: o model binding responde 400, sem nada para varrer.
        }

        http.Request.Body.Position = 0;
        if (raiz is null)
        {
            return [];
        }

        IReadOnlyList<DeteccaoDePii> foraDoLugar = _varredor.VarrerJson(raiz, contexto);
        if (foraDoLugar.Any(d => d.Desfecho == DesfechoDaDeteccao.Mascarado))
        {
            byte[] mascarado = JsonSerializer.SerializeToUtf8Bytes(raiz);
            MemoryStream corpo = new(mascarado, writable: false);
            http.Response.RegisterForDispose(corpo);
            http.Request.Body = corpo;
            http.Request.ContentLength = mascarado.Length;
        }

        return foraDoLugar;
    }

    private static async Task RecusarAsync(HttpContext http, IReadOnlyList<DeteccaoDePii> foraDoLugar)
    {
        http.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        ProblemDetails problema = new()
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Dado pessoal em campo livre",
            Detail = "Tire o dado pessoal dos campos indicados e tente de novo. Os campos próprios (CPF, telefone) continuam valendo.",
            Extensions =
            {
                ["campos"] = foraDoLugar.Select(d => new { campo = d.Campo, categoria = d.Categoria }).Distinct().ToArray(),
            },
        };
        await http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
    }
}
