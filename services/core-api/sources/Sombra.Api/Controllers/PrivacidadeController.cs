using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>O Risco Cidadão para quem opera: o farol executivo e a trilha de segurança (spec 07 §4), só com metadados.</summary>
[ApiController]
[Route("api/v1/privacidade")]
[Authorize(Roles = Acesso.OperadorOuAdmin)]
[Tags("Privacidade")]
public sealed class PrivacidadeController : ControllerBase
{
    private readonly IConsultasDoPainel _consultas;
    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="PrivacidadeController"/> class.</summary>
    /// <param name="consultas">As consultas.</param>
    /// <param name="relogio">O relógio do sistema.</param>
    public PrivacidadeController(IConsultasDoPainel consultas, IRelogio relogio)
    {
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
    }

    /// <summary>O farol de Risco Cidadão, global e por família.</summary>
    /// <remarks>
    /// Cada evento de segurança dos últimos 7 dias vale <c>40 × família × severidade × desfecho × 0,5^(idade/72 h)</c>; a
    /// soma, até 100, dá a cor: verde &lt; 30 ≤ amarelo &lt; 70 ≤ vermelho. Saúde e credencial pesam 1, identificação
    /// 0,8, contato 0,6, localização 0,4; um vazamento vale três bloqueios.
    /// </remarks>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O farol.</returns>
    [HttpGet("risco-cidadao")]
    [ProducesResponseType<RiscoCidadaoResposta>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiscoCidadaoResposta>> RiscoCidadao(CancellationToken cancellationToken)
        => await _consultas.RiscoCidadaoAsync(_relogio.Agora, cancellationToken);

    /// <summary>A trilha de segurança, do mais recente para o mais antigo.</summary>
    /// <param name="filtro">A página.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>Os eventos, sem nenhum dado pessoal.</returns>
    [HttpGet("eventos")]
    [ProducesResponseType<Paginado<EventoDeSegurancaResposta>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<Paginado<EventoDeSegurancaResposta>>> Eventos([FromQuery] ConsultaPaginadaDto filtro, CancellationToken cancellationToken)
        => await _consultas.ListarEventosDeSegurancaAsync(filtro, cancellationToken);
}
