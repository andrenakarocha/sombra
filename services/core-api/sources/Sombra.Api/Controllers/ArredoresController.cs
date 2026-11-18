using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Arredores;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>O que o app mostra em volta da casa: os lugares frescos por perto e o farol de privacidade.</summary>
[ApiController]
[Route("api/v1/domicilios/{id:guid}")]
[Authorize(Roles = Acesso.Todos, Policy = CasaDoCuidador.Politica)]
[Tags("Perto de mim e privacidade")]
public sealed class ArredoresController : ControllerBase
{
    private readonly PontosDeResfriamentoDaCasa _pontos;
    private readonly IConsultasDoPainel _consultas;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="ArredoresController"/> class.</summary>
    /// <param name="pontos">Os pontos de resfriamento da casa.</param>
    /// <param name="consultas">As consultas de leitura.</param>
    /// <param name="relogio">O relógio.</param>
    public ArredoresController(PontosDeResfriamentoDaCasa pontos, IConsultasDoPainel consultas, IRelogio relogio)
    {
        _pontos = pontos ?? throw new ArgumentNullException(nameof(pontos));
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>Os pontos de resfriamento perto da casa.</summary>
    /// <remarks>
    /// Do mais perto ao mais longe, dentro do raio (padrão de 3 km), medidos da estação do INMET da região climática da
    /// casa. Por enquanto a lista é de exemplo, rotulada na <c>fonte</c>; na F3.9 ela vem do Clima, no mesmo formato.
    /// </remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="filtro">O raio.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Os pontos.</returns>
    [HttpGet("pontos-de-resfriamento")]
    [ProducesResponseType<IReadOnlyList<PontoDeResfriamentoResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<PontoDeResfriamentoResposta>>> Pontos(Guid id, [FromQuery] FiltroDePontosDto filtro, CancellationToken cancellationToken)
        => Ok(await _pontos.ExecutarAsync(id, filtro, cancellationToken));

    /// <summary>O farol de privacidade da casa e as mensagens para o cidadão.</summary>
    /// <remarks>
    /// Conta os eventos de segurança das Sentinelas da casa na última semana: nenhum é verde, um ou dois é amarelo, três
    /// ou mais é vermelho. As mensagens vêm de templates, sem dado pessoal; a F2.5 troca o cálculo e a F2.6 traz a tradução
    /// da IA, sem mudar o formato.
    /// </remarks>
    /// <param name="id" example="0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10">A casa.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O farol e as mensagens.</returns>
    [HttpGet("privacidade")]
    [ProducesResponseType<PrivacidadeDaCasaResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PrivacidadeDaCasaResposta>> Privacidade(Guid id, CancellationToken cancellationToken)
        => await _consultas.PrivacidadeDaCasaAsync(id, _relogio.Agora, cancellationToken);
}
