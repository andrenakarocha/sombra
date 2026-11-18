using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>Os números do painel da operação.</summary>
[ApiController]
[Route("api/v1/painel")]
[Authorize(Roles = Acesso.OperadorOuAdmin)]
[Tags("Painel")]
public sealed class PainelController : ControllerBase
{
    private readonly IConsultasDoPainel _consultas;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="PainelController"/> class.</summary>
    /// <param name="consultas">As consultas.</param>
    /// <param name="relogio">O relógio.</param>
    public PainelController(IConsultasDoPainel consultas, IRelogio relogio)
    {
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>O resumo do painel e os indicadores de impacto.</summary>
    /// <remarks>
    /// Casas monitoradas, alertas ativos por nível, mediana até o "estou bem", alertas resolvidos antes de chamar a rede toda,
    /// alarmes desnecessários, escalonamentos de hoje por degrau, Sentinelas offline e alertas da semana por bairro.
    /// </remarks>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O resumo.</returns>
    [HttpGet("resumo")]
    [ProducesResponseType<PainelResumoResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PainelResumoResposta>> Resumo(CancellationToken cancellationToken)
        => await _consultas.ObterResumoAsync(_relogio.Agora, cancellationToken);
}
