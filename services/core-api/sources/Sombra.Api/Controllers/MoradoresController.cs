using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>Os moradores de uma casa, sempre com CPF, Cartão SUS e saúde mascarados.</summary>
[ApiController]
[Route("api/v1/domicilios/{id:guid}/moradores")]
[Authorize(Roles = Acesso.CuidadorOuAdmin, Policy = CasaDoCuidador.Politica)]
[Tags("Moradores")]
public sealed class MoradoresController : ControllerBase
{
    private readonly CadastroDeDomicilios _cadastro;

    /// <summary>Initializes a new instance of the <see cref="MoradoresController"/> class.</summary>
    /// <param name="cadastro">O cadastro das casas.</param>
    public MoradoresController(CadastroDeDomicilios cadastro) => _cadastro = cadastro ?? throw new ArgumentNullException(nameof(cadastro));

    /// <summary>Lista os moradores.</summary>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Os moradores.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MoradorResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<MoradorResposta>>> Listar(Guid id, CancellationToken cancellationToken)
        => Ok(await _cadastro.ListarMoradoresAsync(id, User.IsInRole(Papeis.Cuidador), cancellationToken));

    /// <summary>Cadastra um morador.</summary>
    /// <remarks>Regra R1: CPF e Cartão SUS válidos pelo dígito verificador, sem CPF repetido na casa. No máximo quatro moradores.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">Os dados do morador.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O morador, com a PII mascarada.</returns>
    [HttpPost]
    [ProducesResponseType<MoradorResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MoradorResposta>> Cadastrar(Guid id, NovoMoradorDto dto, CancellationToken cancellationToken)
    {
        MoradorResposta morador = await _cadastro.AdicionarMoradorAsync(id, dto, User.IsInRole(Papeis.Cuidador), cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id, moradorId = morador.Id }, morador);
    }

    /// <summary>Um morador.</summary>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="moradorId" example="c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10">O morador.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O morador.</returns>
    [HttpGet("{moradorId:guid}")]
    [ProducesResponseType<MoradorResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MoradorResposta>> Obter(Guid id, Guid moradorId, CancellationToken cancellationToken)
        => await _cadastro.ObterMoradorAsync(id, moradorId, User.IsInRole(Papeis.Cuidador), cancellationToken);

    /// <summary>Atualiza os fatores de risco declarados.</summary>
    /// <remarks>Regra R3: os fatores reduzem os limites térmicos da casa. A idade avançada vem da data de nascimento.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="moradorId" example="c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10">O morador.</param>
    /// <param name="dto">Os fatores.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O morador.</returns>
    [HttpPut("{moradorId:guid}")]
    [ProducesResponseType<MoradorResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MoradorResposta>> AtualizarPerfil(Guid id, Guid moradorId, PerfilDoMoradorDto dto, CancellationToken cancellationToken)
        => await _cadastro.AtualizarPerfilDoMoradorAsync(id, moradorId, dto, User.IsInRole(Papeis.Cuidador), cancellationToken);

    /// <summary>Remove um morador.</summary>
    /// <remarks>Regra R8: o último morador de uma casa monitorada só sai depois de desligar o monitoramento.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="moradorId" example="c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10">O morador.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Nada.</returns>
    [HttpDelete("{moradorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Remover(Guid id, Guid moradorId, CancellationToken cancellationToken)
    {
        await _cadastro.RemoverMoradorAsync(id, moradorId, cancellationToken);
        return NoContent();
    }
}
