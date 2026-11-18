using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sombra.Api.TempoReal;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Api.Privacidade;

/// <summary>
/// O guarda do sink de resposta HTTP (spec 07 §2.2): fica por fora do tratador de erros, então vê toda resposta, inclusive
/// ProblemDetails, stack trace que escapasse e erro do EF. Se o corpo leva dado pessoal (pelo taint ou pelo conteúdo) de
/// uma família que o endpoint não declara em <see cref="PermitePiiAttribute"/> para quem pede, a resposta é trocada por
/// um erro seguro com o <c>correlationId</c> e o vazamento vai para a fila. Um 401/403 num endpoint
/// <see cref="DadosPessoaisAttribute"/> também é registrado (A3).
/// </summary>
internal sealed class TaintResponseGuardMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly GuardaDeSaida _guarda;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ModoDaPrivacidade _modo;

    /// <summary>Initializes a new instance of the <see cref="TaintResponseGuardMiddleware"/> class.</summary>
    /// <param name="proximo">O resto do pipeline.</param>
    /// <param name="guarda">A guarda de saída.</param>
    /// <param name="observador">Quem registra o bloqueio.</param>
    /// <param name="modo">Se bloqueia ou só registra (modo inseguro da auditoria).</param>
    public TaintResponseGuardMiddleware(RequestDelegate proximo, GuardaDeSaida guarda, IObservadorDeVazamentos observador, ModoDaPrivacidade modo)
    {
        _proximo = proximo;
        _guarda = guarda;
        _observador = observador;
        _modo = modo;
    }

    /// <summary>Guarda a resposta.</summary>
    /// <param name="http">A requisição.</param>
    /// <returns>O processamento.</returns>
    public async Task InvokeAsync(HttpContext http)
    {
        if (http.WebSockets.IsWebSocketRequest || http.Request.Path.StartsWithSegments(HubDoSombra.Rota, StringComparison.Ordinal))
        {
            await _proximo(http);
            return;
        }

        Stream original = http.Response.Body;
        await using MemoryStream buffer = new();
        http.Response.Body = buffer;
        try
        {
            await _proximo(http);
        }
        finally
        {
            http.Response.Body = original;
        }

        Endpoint? endpoint = http.GetEndpoint();
        string rota = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
        RegistrarAcessoNegado(http, endpoint, rota);

        ResultadoDaGuarda resultado = EhTexto(http.Response.ContentType) && buffer.Length > 0
            ? _guarda.Verificar(Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), ContextoDeTaint.Atual)
            : new ResultadoDaGuarda([], string.Empty);
        HashSet<CategoriaDePii> permitidas = [.. endpoint?.Metadata.GetOrderedMetadata<PermitePiiAttribute>().Where(p => p.Vale(http.User)).Select(p => p.Categoria) ?? []];
        IReadOnlyList<AchadoNaSaida> fora = resultado.Fora(permitidas);
        foreach (AchadoNaSaida a in fora.Distinct())
        {
            _observador.Registrar(new VazamentoBloqueado(SinkDePii.RespostaHttp, rota, a.RegraId, a.Categoria, a.Severidade, a.PeloTaint, http.TraceIdentifier, Bloqueado: _modo.Bloqueia));
        }

        if (fora.Count == 0 || !_modo.Bloqueia)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(original, http.RequestAborted);
            return;
        }

        // Quem não se identificou recebe 401 sem dado nenhum (A3); um erro mantém o status; um sucesso vira 500.
        int status = http.User.Identity?.IsAuthenticated != true ? StatusCodes.Status401Unauthorized
            : http.Response.StatusCode >= StatusCodes.Status400BadRequest ? http.Response.StatusCode
            : StatusCodes.Status500InternalServerError;
        http.Response.Clear();
        http.Response.StatusCode = status;
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = "Resposta bloqueada",
                Detail = "A resposta levava dado pessoal e foi bloqueada para proteger o cidadão. Informe o correlationId ao suporte.",
            },
        });
    }

    private static bool EhTexto(string? tipo)
        => tipo is not null && (tipo.Contains("json", StringComparison.OrdinalIgnoreCase) || tipo.StartsWith("text/", StringComparison.OrdinalIgnoreCase));

    private void RegistrarAcessoNegado(HttpContext http, Endpoint? endpoint, string rota)
    {
        if (http.Response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
            || endpoint?.Metadata.GetMetadata<DadosPessoaisAttribute>() is not { } dados)
        {
            return;
        }

        foreach (CategoriaDePii categoria in dados.Categorias)
        {
            _observador.Registrar(new VazamentoBloqueado(SinkDePii.RespostaHttp, rota, "acesso-negado", categoria, SeveridadeDePii.Alta, PeloTaint: false, http.TraceIdentifier));
        }
    }
}
