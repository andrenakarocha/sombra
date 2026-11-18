using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.Controllers;

/// <summary>Os alertas de calor e a resposta do cuidador.</summary>
[ApiController]
[Route("api/v1/alertas")]
[Authorize]
[Tags("Alertas")]
public sealed class AlertasController : ControllerBase
{
    private readonly IConsultasDoPainel _consultas;
    private readonly ContasDeAcesso _contas;
    private readonly ReconhecerAlerta _reconhecer;

    /// <summary>Initializes a new instance of the <see cref="AlertasController"/> class.</summary>
    /// <param name="consultas">As consultas.</param>
    /// <param name="contas">As contas de acesso.</param>
    /// <param name="reconhecer">O caso de uso da resposta do cuidador.</param>
    public AlertasController(IConsultasDoPainel consultas, ContasDeAcesso contas, ReconhecerAlerta reconhecer)
    {
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _contas = contas ?? throw new ArgumentNullException(nameof(contas));
        _reconhecer = reconhecer ?? throw new ArgumentNullException(nameof(reconhecer));
    }

    /// <summary>Lista os alertas, do mais recente ao mais antigo.</summary>
    /// <remarks>O Cuidador vê só os das casas dele.</remarks>
    /// <param name="filtro">Estado, nível, página e tamanho.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Uma página de alertas.</returns>
    [HttpGet]
    [Authorize(Roles = Acesso.Todos)]
    [ProducesResponseType<Paginado<AlertaResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<Paginado<AlertaResposta>>> Listar([FromQuery] FiltroDeAlertasDto filtro, CancellationToken cancellationToken)
        => await _consultas.ListarAlertasAsync(filtro, await CasasVisiveisAsync(cancellationToken), cancellationToken);

    /// <summary>Um alerta.</summary>
    /// <param name="id" example="5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55">O alerta.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O alerta.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Acesso.Todos)]
    [ProducesResponseType<AlertaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AlertaResposta>> Obter(Guid id, CancellationToken cancellationToken)
    {
        AlertaResposta alerta = await AlertaVisivelAsync(id, cancellationToken);
        return await PodeVerAsync(alerta.DomicilioId, cancellationToken) ? alerta : Forbid();
    }

    /// <summary>Responde a um alerta.</summary>
    /// <remarks>
    /// "VOU_LIGAR" e "ESTOU_INDO" param o escalonamento; "ELA_ESTA_BEM" encerra. Regra R5: um alerta encerrado não muda mais (422).
    /// </remarks>
    /// <param name="id" example="5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55">O alerta.</param>
    /// <param name="dto">A ação.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O alerta depois da resposta.</returns>
    [HttpPost("{id:guid}/reconhecer")]
    [Authorize(Roles = Papeis.Cuidador)]
    [ProducesResponseType<AlertaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<AlertaResposta>> Reconhecer(Guid id, ReconhecimentoDto dto, CancellationToken cancellationToken)
    {
        AlertaResposta alerta = await AlertaVisivelAsync(id, cancellationToken);
        return await PodeVerAsync(alerta.DomicilioId, cancellationToken)
            ? await _reconhecer.ExecutarAsync(id, dto, cancellationToken)
            : Forbid();
    }

    private async Task<AlertaResposta> AlertaVisivelAsync(Guid id, CancellationToken cancellationToken)
        => await _consultas.ObterAlertaAsync(id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Alerta não encontrado.");

    private async Task<bool> PodeVerAsync(Guid casa, CancellationToken cancellationToken)
        => await CasasVisiveisAsync(cancellationToken) is not { } casas || casas.Contains(casa);

    private async Task<IReadOnlyList<Guid>?> CasasVisiveisAsync(CancellationToken cancellationToken)
        => User.IsInRole(Papeis.Cuidador) ? await _contas.CasasDoUsuarioAsync(User.Id(), cancellationToken) : null;
}
