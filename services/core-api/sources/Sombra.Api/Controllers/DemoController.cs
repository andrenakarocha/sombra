using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Infraestrutura;

namespace Sombra.Api.Controllers;

/// <summary>O relógio do Core adiantado depois do avanço.</summary>
/// <param name="Agora">O novo agora do Core.</param>
public sealed record RelogioResposta(DateTimeOffset Agora);

/// <summary>
/// Atalhos da demonstração: só existem nos ambientes <c>Development</c> e <c>Demo</c> (404 nos outros) e só para Admin.
/// </summary>
[ApiController]
[Route("api/v1/demo")]
[Authorize(Roles = Papeis.Admin)]
[Tags("Demonstração")]
public sealed class DemoController : ControllerBase
{
    private readonly RelogioDoSistema _relogio;
    private readonly IWebHostEnvironment _ambiente;

    /// <summary>Initializes a new instance of the <see cref="DemoController"/> class.</summary>
    /// <param name="relogio">O relógio do Core.</param>
    /// <param name="ambiente">O ambiente.</param>
    public DemoController(RelogioDoSistema relogio, IWebHostEnvironment ambiente)
    {
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _ambiente = ambiente ?? throw new ArgumentNullException(nameof(ambiente));
    }

    /// <summary>Adianta o relógio do Core.</summary>
    /// <remarks>
    /// Para o caminho B do roteiro caber num vídeo: adiantar 15 minutos vence o prazo do "estou bem" e o escalonador sobe
    /// o primeiro degrau na volta seguinte. A janela anti-replay das mensagens da Sentinela continua no tempo real.
    /// </remarks>
    /// <param name="dto">Os minutos.</param>
    /// <returns>O novo agora.</returns>
    [HttpPost("relogio/avancar")]
    [ProducesResponseType<RelogioResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<RelogioResposta> AvancarRelogio(AvancoDoRelogioDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!_ambiente.IsDevelopment() && !_ambiente.IsEnvironment("Demo"))
        {
            return NotFound();
        }

        return new RelogioResposta(_relogio.Avancar(TimeSpan.FromMinutes(dto.Minutos.GetValueOrDefault())));
    }
}
