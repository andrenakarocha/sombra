using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sombra.Api.Composicao;

namespace Sombra.Api.Controllers;

/// <summary>O estado de saúde de um serviço.</summary>
/// <param name="Status">O estado geral: <c>Healthy</c>, <c>Degraded</c> ou <c>Unhealthy</c>.</param>
/// <param name="Verificacoes">O estado de cada dependência.</param>
public sealed record SaudeResposta(string Status, IReadOnlyDictionary<string, string> Verificacoes);

/// <summary>A versão em execução (o mesmo <c>commit</c> dos logs, §6 do plano).</summary>
/// <param name="Versao">A versão do assembly.</param>
/// <param name="Commit">O commit do build.</param>
/// <param name="ConstruidoEm">Quando o build foi feito.</param>
public sealed record VersaoResposta(string Versao, string Commit, string ConstruidoEm);

/// <summary>Saúde e versão, públicas e fora do limite de requisições (o monitor da VM e o compose as consultam).</summary>
[ApiController]
[AllowAnonymous]
[DisableRateLimiting]
[Tags("Sistema")]
public sealed class SistemaController : ControllerBase
{
    private readonly HealthCheckService _saude;
    private readonly IConfiguration _configuracao;

    /// <summary>Initializes a new instance of the <see cref="SistemaController"/> class.</summary>
    /// <param name="saude">Os health checks.</param>
    /// <param name="configuracao">A configuração (commit e data do build).</param>
    public SistemaController(HealthCheckService saude, IConfiguration configuracao)
    {
        _saude = saude ?? throw new ArgumentNullException(nameof(saude));
        _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));
    }

    /// <summary>O processo está vivo.</summary>
    /// <remarks>Não consulta nada: serve para o systemd e o compose saberem se o processo responde.</remarks>
    /// <returns>Sempre <c>Healthy</c>.</returns>
    [HttpGet("/health/live")]
    [ProducesResponseType<SaudeResposta>(StatusCodes.Status200OK)]
    public ActionResult<SaudeResposta> Vivo() => new SaudeResposta(nameof(HealthStatus.Healthy), new Dictionary<string, string>());

    /// <summary>O Core está pronto para atender.</summary>
    /// <remarks>Confere o banco (o broker MQTT entra na F1.9). Responde 503 quando alguma dependência falha.</remarks>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O estado de cada dependência.</returns>
    [HttpGet("/health/ready")]
    [ProducesResponseType<SaudeResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<SaudeResposta>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SaudeResposta>> Pronto(CancellationToken cancellationToken)
    {
        HealthReport relatorio = await _saude.CheckHealthAsync(c => c.Tags.Contains(ComposicaoDoCore.TagPronto), cancellationToken);
        SaudeResposta resposta = new(relatorio.Status.ToString(), relatorio.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString(), StringComparer.Ordinal));
        return relatorio.Status == HealthStatus.Healthy ? resposta : StatusCode(StatusCodes.Status503ServiceUnavailable, resposta);
    }

    /// <summary>A versão em execução.</summary>
    /// <returns>Versão, commit e data do build.</returns>
    [HttpGet("/version")]
    [ProducesResponseType<VersaoResposta>(StatusCodes.Status200OK)]
    public ActionResult<VersaoResposta> Versao() => VersaoDoBuild.De(_configuracao);
}
