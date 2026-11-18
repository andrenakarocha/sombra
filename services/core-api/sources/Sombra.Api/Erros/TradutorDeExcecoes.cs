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
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

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
    private readonly GuardaDeSaida _guarda;
    private readonly IObservadorDeVazamentos _observador;
    private readonly ModoDaPrivacidade _modo;

    /// <summary>Initializes a new instance of the <see cref="TradutorDeExcecoes"/> class.</summary>
    /// <param name="problemas">Quem escreve o ProblemDetails.</param>
    /// <param name="log">O log, para as exceções inesperadas.</param>
    /// <param name="guarda">A guarda de saída: a mensagem de uma exceção esperada vai para a resposta sem PII.</param>
    /// <param name="observador">Quem registra a mensagem redigida.</param>
    /// <param name="modo">Se redige ou só registra (modo inseguro da auditoria).</param>
    public TradutorDeExcecoes(IProblemDetailsService problemas, ILogger<TradutorDeExcecoes> log, GuardaDeSaida guarda, IObservadorDeVazamentos observador, ModoDaPrivacidade modo)
    {
        _problemas = problemas ?? throw new ArgumentNullException(nameof(problemas));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _guarda = guarda ?? throw new ArgumentNullException(nameof(guarda));
        _observador = observador ?? throw new ArgumentNullException(nameof(observador));
        _modo = modo ?? throw new ArgumentNullException(nameof(modo));
    }

    /// <summary>Diz se a exceção é esperada (uma resposta 4xx), e não um defeito para o log de erro.</summary>
    /// <param name="excecao">A exceção.</param>
    /// <returns><c>true</c> para as exceções que viram 4xx.</returns>
    public static bool EhEsperada(Exception excecao)
        => excecao is ValidacaoException or RecursoNaoEncontradoException or ConflitoException or RegraVioladaException or SaidaBloqueadaException;

    /// <inheritdoc/>
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        string rota = (httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";
        if (!EhEsperada(exception))
        {
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
            SaidaBloqueadaException => Problema(StatusCodes.Status422UnprocessableEntity, "Saída bloqueada", "O pedido levaria dado pessoal para fora do Sombra e foi bloqueado."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Erro inesperado", null),
        };
        problema.Type = Tipo;
        problema.Detail = SemPii(problema.Detail, rota, httpContext);
        httpContext.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        return _problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problema, Exception = exception });
    }

    // O guarda do sink de exceções (spec 07 §2.4): a mensagem de uma regra violada pode ter sido montada com o dado.
    private string? SemPii(string? detalhe, string rota, HttpContext http)
    {
        ResultadoDaGuarda resultado = _guarda.Verificar(detalhe, ContextoDeTaint.Atual);
        foreach (AchadoNaSaida a in resultado.Achados)
        {
            _observador.Registrar(new VazamentoBloqueado(SinkDePii.Excecao, rota, a.RegraId, a.Categoria, a.Severidade, a.PeloTaint, http.TraceIdentifier, Bloqueado: _modo.Bloqueia));
        }

        return resultado.Limpo || !_modo.Bloqueia ? detalhe : resultado.Redigido;
    }

    private static ProblemDetails Problema(int status, string titulo, string? detalhe) => new() { Status = status, Title = titulo, Detail = detalhe };

    private static ProblemDetails ComRegra(ProblemDetails problema, string regra)
    {
        problema.Extensions["regra"] = regra;
        return problema;
    }
}
