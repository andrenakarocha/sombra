using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>A rede de cuidado (quem o escalonamento procura) e a rotina combinada com a pessoa.</summary>
[ApiController]
[Route("api/v1/domicilios/{id:guid}")]
[Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
[Tags("Rede de cuidado e rotina")]
public sealed class CuidadoController : ControllerBase
{
    private readonly CadastroDeDomicilios _cadastro;

    /// <summary>Initializes a new instance of the <see cref="CuidadoController"/> class.</summary>
    /// <param name="cadastro">O cadastro das casas.</param>
    public CuidadoController(CadastroDeDomicilios cadastro) => _cadastro = cadastro ?? throw new ArgumentNullException(nameof(cadastro));

    /// <summary>A rede de cuidado, na ordem de escalonamento.</summary>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A rede, com os telefones mascarados.</returns>
    [HttpGet("rede-de-cuidado")]
    [ProducesResponseType<RedeDeCuidadoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RedeDeCuidadoResposta>> ObterRede(Guid id, CancellationToken cancellationToken)
        => await _cadastro.ObterRedeDeCuidadoAsync(id, cancellationToken);

    /// <summary>Substitui a rede de cuidado inteira.</summary>
    /// <remarks>
    /// D13: o primeiro FAMILIAR recebe o lembrete leve, o primeiro VIZINHO o "pode dar uma olhada?", e a rede toda o alerta.
    /// Regra R8: até cinco contatos, sem telefone repetido, e uma casa monitorada não fica sem nenhum.
    /// </remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">Os contatos, do primeiro ao último.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A rede nova.</returns>
    [HttpPut("rede-de-cuidado")]
    [ProducesResponseType<RedeDeCuidadoResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RedeDeCuidadoResposta>> DefinirRede(Guid id, RedeDeCuidadoDto dto, CancellationToken cancellationToken)
        => await _cadastro.DefinirRedeDeCuidadoAsync(id, dto, cancellationToken);

    /// <summary>A rotina combinada: check-in diário e lembrete de água.</summary>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A rotina.</returns>
    [HttpGet("rotina")]
    [ProducesResponseType<RotinaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RotinaResposta>> ObterRotina(Guid id, CancellationToken cancellationToken)
        => await _cadastro.ObterRotinaAsync(id, cancellationToken);

    /// <summary>Substitui a rotina.</summary>
    /// <remarks>Regras R11 e R12: o check-in diário é opcional e nunca entre 22h e 7h; esquecer gera só um lembrete leve.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">A rotina.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A rotina nova.</returns>
    [HttpPut("rotina")]
    [ProducesResponseType<RotinaResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<RotinaResposta>> DefinirRotina(Guid id, RotinaDto dto, CancellationToken cancellationToken)
        => await _cadastro.DefinirRotinaAsync(id, dto, cancellationToken);
}
