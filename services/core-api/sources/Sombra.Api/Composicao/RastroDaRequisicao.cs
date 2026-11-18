using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Comum;

namespace Sombra.Api.Composicao;

/// <summary>
/// A correlação ponta a ponta (<c>docs/observabilidade.md</c> §3) e o log de cada requisição. Aceita o header
/// <c>X-Correlation-Id</c> (até 64 caracteres em <c>[A-Za-z0-9:_-]</c>) ou gera 16 hex, devolve o mesmo header, abre o
/// escopo do log e registra <c>requisicao.concluida</c> com a rota-modelo, sem corpo nem IDs na rota.
/// </summary>
public sealed partial class RastroDaRequisicao
{
    /// <summary>O header da correlação.</summary>
    public const string Cabecalho = "X-Correlation-Id";

    private readonly RequestDelegate _proximo;
    private readonly ILogger<RastroDaRequisicao> _log;

    /// <summary>Initializes a new instance of the <see cref="RastroDaRequisicao"/> class.</summary>
    /// <param name="proximo">O resto do pipeline.</param>
    /// <param name="log">O log.</param>
    public RastroDaRequisicao(RequestDelegate proximo, ILogger<RastroDaRequisicao> log)
    {
        _proximo = proximo ?? throw new ArgumentNullException(nameof(proximo));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Atende a requisição.</summary>
    /// <param name="contexto">A requisição.</param>
    /// <returns>O atendimento.</returns>
    public async Task InvokeAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        string recebido = contexto.Request.Headers[Cabecalho].ToString();
        string id = FormatoValido().IsMatch(recebido) ? recebido : Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        contexto.TraceIdentifier = id;
        Correlacao.Atual = id;
        contexto.Response.OnStarting(() =>
        {
            contexto.Response.Headers[Cabecalho] = id;
            return Task.CompletedTask;
        });

        using IDisposable? escopo = _log.BeginScope(new Dictionary<string, object> { [LogDoSombra.CorrelationId] = id });
        long inicio = Stopwatch.GetTimestamp();
        try
        {
            await _proximo(contexto);
        }
        finally
        {
            string rota = (contexto.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
            int status = contexto.Response.StatusCode;
            LogLevel nivel = status >= StatusCodes.Status500InternalServerError ? LogLevel.Error
                : rota.StartsWith("/health", StringComparison.Ordinal) ? LogLevel.Debug
                : LogLevel.Information;
            if (_log.IsEnabled(nivel))
            {
                double duracaoMs = Math.Round(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, 1);
                _log.RequisicaoConcluida(nivel, contexto.Request.Method, rota, status, duracaoMs);
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9:_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoValido();
}
