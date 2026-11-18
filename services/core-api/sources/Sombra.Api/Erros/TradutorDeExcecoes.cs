using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Sombra.Api.Composicao;
using Sombra.Aplicacao.Comum;
using Sombra.Dominio.Comum;

namespace Sombra.Api.Erros;

/// <summary>
/// Traduz as exceções da aplicação e do domínio em ProblemDetails (RFC 9457): validação 400, recurso 404, conflito 409,
/// regra de negócio 422 (com o código da regra) e o resto 500, sem detalhe. O <c>detail</c> nunca tem dado pessoal.
/// </summary>
public sealed class TradutorDeExcecoes : IExceptionHandler
{
    /// <summary>O endereço da explicação dos erros no contrato.</summary>
    public const string Tipo = "https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/core-api.md#erros";

    private readonly IProblemDetailsService _problemas;
    private readonly ILogger<TradutorDeExcecoes> _log;

    /// <summary>Initializes a new instance of the <see cref="TradutorDeExcecoes"/> class.</summary>
    /// <param name="problemas">Quem escreve o ProblemDetails.</param>
    /// <param name="log">O log, para as exceções inesperadas.</param>
    public TradutorDeExcecoes(IProblemDetailsService problemas, ILogger<TradutorDeExcecoes> log)
    {
        _problemas = problemas ?? throw new ArgumentNullException(nameof(problemas));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Diz se a exceção é esperada (uma resposta 4xx), e não um defeito para o log de erro.</summary>
    /// <param name="excecao">A exceção.</param>
    /// <returns><c>true</c> para as exceções que viram 4xx.</returns>
    public static bool EhEsperada(Exception excecao)
        => excecao is ValidacaoException or RecursoNaoEncontradoException or ConflitoException or RegraVioladaException;

    /// <inheritdoc/>
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (!EhEsperada(exception))
        {
            string rota = (httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
            _log.RequisicaoFalhou(exception, httpContext.Request.Method, rota);
        }

        ProblemDetails problema = exception switch
        {
            ValidacaoException v => new ValidationProblemDetails(v.Erros.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Dados inválidos",
            },
            RecursoNaoEncontradoException => Problema(StatusCodes.Status404NotFound, "Recurso não encontrado", exception.Message),
            ConflitoException => Problema(StatusCodes.Status409Conflict, "Conflito com o estado atual", exception.Message),
            RegraVioladaException r => ComRegra(Problema(StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", r.Message), r.Regra),
            _ => Problema(StatusCodes.Status500InternalServerError, "Erro inesperado", null),
        };
        problema.Type = Tipo;
        httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return _problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problema, Exception = exception });
    }

    private static ProblemDetails Problema(int status, string titulo, string? detalhe) => new() { Status = status, Title = titulo, Detail = detalhe };

    private static ProblemDetails ComRegra(ProblemDetails problema, string regra)
    {
        problema.Extensions["regra"] = regra;
        return problema;
    }
}
