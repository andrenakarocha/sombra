using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>A Sentinela da casa e as leituras dela.</summary>
[ApiController]
[Route("api/v1/domicilios/{id:guid}")]
[Authorize(Policy = CasaDoCuidador.Politica)]
[Tags("Sentinelas e leituras")]
public sealed class SentinelasController : ControllerBase
{
    private readonly CadastroDeDomicilios _cadastro;
    private readonly IConsultasDoPainel _consultas;

    /// <summary>Initializes a new instance of the <see cref="SentinelasController"/> class.</summary>
    /// <param name="cadastro">O cadastro das casas.</param>
    /// <param name="consultas">As consultas.</param>
    public SentinelasController(CadastroDeDomicilios cadastro, IConsultasDoPainel consultas)
    {
        _cadastro = cadastro ?? throw new ArgumentNullException(nameof(cadastro));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
    }

    /// <summary>As Sentinelas da casa e se estão online.</summary>
    /// <remarks>Regra R9: a Sentinela fica offline depois de três telemetrias perdidas (30 segundos).</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>As Sentinelas.</returns>
    [HttpGet("sentinelas")]
    [Authorize(Roles = Acesso.Todos)]
    [ProducesResponseType<IReadOnlyList<SentinelaResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<SentinelaResposta>>> Listar(Guid id, CancellationToken cancellationToken)
        => Ok(await _cadastro.ListarSentinelasAsync(id, cancellationToken));

    /// <summary>Vincula a Sentinela da casa.</summary>
    /// <remarks>
    /// O app lê o QR da Sentinela (<c>sombra://sentinela/vincular?v=1&amp;d=…&amp;c=…</c>) e manda o identificador e o código
    /// de vínculo, que só confere com a Sentinela na mão (422 com regra <c>VINCULO</c> quando não confere). Uma Sentinela por
    /// casa, e uma casa por Sentinela (409 se ela já for de outra casa). A resposta traz, uma única vez, o segredo HMAC que
    /// assina as mensagens da Sentinela (contrato MQTT).
    /// </remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="dto">O identificador da Sentinela.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>A Sentinela vinculada, com o segredo.</returns>
    [HttpPost("sentinelas")]
    [Authorize(Roles = Acesso.CuidadorOuAdmin)]
    [ProducesResponseType<SentinelaVinculadaResposta>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<SentinelaVinculadaResposta>> Vincular(Guid id, NovaSentinelaDto dto, CancellationToken cancellationToken)
    {
        SentinelaVinculadaResposta sentinela = await _cadastro.VincularSentinelaAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(Listar), new { id }, sentinela);
    }

    /// <summary>As leituras de um período, da mais recente à mais antiga.</summary>
    /// <remarks>O período é fechado no começo e aberto no fim. O índice de calor é o calculado pelo Core.</remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="filtro">De, até, página e tamanho.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Uma página de leituras.</returns>
    [HttpGet("leituras")]
    [Authorize(Roles = Acesso.Todos)]
    [ProducesResponseType<Paginado<LeituraResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<Paginado<LeituraResposta>>> ListarLeituras(Guid id, [FromQuery] FiltroDeLeiturasDto filtro, CancellationToken cancellationToken)
        => await _consultas.ListarLeiturasAsync(id, filtro, cancellationToken);
}
