using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sombra.Api.Erros;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Api.Controllers;

/// <summary>O monitor da VM avisa o painel de um problema de infraestrutura.</summary>
[ApiController]
[Route("api/v1/ops")]
[AllowAnonymous]
[Tags("Operação")]
public sealed partial class OperacaoController : ControllerBase
{
    private readonly ITempoReal _tempoReal;
    private readonly IConfiguration _configuracao;
    private readonly ILogger<OperacaoController> _log;

    /// <summary>Initializes a new instance of the <see cref="OperacaoController"/> class.</summary>
    /// <param name="tempoReal">O hub.</param>
    /// <param name="configuracao">A configuração (<c>Ops:TokenDoMonitor</c>).</param>
    /// <param name="log">O log.</param>
    public OperacaoController(ITempoReal tempoReal, IConfiguration configuracao, ILogger<OperacaoController> log)
    {
        _tempoReal = tempoReal ?? throw new ArgumentNullException(nameof(tempoReal));
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Recebe um alerta de infraestrutura e o empurra para o painel.</summary>
    /// <remarks>
    /// Autenticação pelo token do monitor (<c>Ops__TokenDoMonitor</c> do <c>.env</c>), no header
    /// <c>Authorization: Bearer …</c>. O painel recebe o evento <c>AlertaDeInfra</c> pelo hub.
    /// </remarks>
    /// <param name="dto">O alerta.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>202 quando aceito.</returns>
    [HttpPost("alertas")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> ReceberAlerta(AlertaDeInfraDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!TokenDoMonitorConfere())
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado", detail: "Token do monitor ausente ou errado.", type: TradutorDeExcecoes.Tipo);
        }

        AlertaDeInfraRecebido(_log, dto.Host, dto.Recurso, dto.Severidade);
        await _tempoReal.PublicarAsync(EventosDeTempoReal.AlertaDeInfra, null, new AlertaDeInfraEvento(dto.Host, dto.Recurso, dto.Severidade, dto.Mensagem, dto.Momento.GetValueOrDefault(), Correlacao.Atual), cancellationToken);
        return Accepted();
    }

    private bool TokenDoMonitorConfere()
    {
        string esperado = _configuracao["Ops:TokenDoMonitor"] ?? string.Empty;
        string recebido = Request.Headers.Authorization.ToString();
        const string prefixo = "Bearer ";
        if (esperado.Length < 16 || !recebido.StartsWith(prefixo, StringComparison.Ordinal))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(recebido[prefixo.Length..]), Encoding.UTF8.GetBytes(esperado));
    }

    [LoggerMessage(EventId = 1101, EventName = "infra.alerta_recebido", Level = LogLevel.Warning, Message = "Alerta de infraestrutura {severidade} em {host}: {recurso}")]
    private static partial void AlertaDeInfraRecebido(ILogger logger, string host, string recurso, string severidade);
}
