using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sombra.Api.Erros;
using Sombra.Infraestrutura.Mqtt;

namespace Sombra.Api.Controllers;

/// <summary>A alternativa por HTTP ao MQTT: a mesma telemetria assinada, conferida do mesmo jeito.</summary>
[ApiController]
[Route("api/v1/sentinelas")]
[AllowAnonymous]
[Tags("Sentinelas e leituras")]
public sealed class TelemetriaController : ControllerBase
{
    private readonly ProcessadorDaSentinela _processador;

    /// <summary>Initializes a new instance of the <see cref="TelemetriaController"/> class.</summary>
    /// <param name="processador">O processador das mensagens da Sentinela.</param>
    public TelemetriaController(ProcessadorDaSentinela processador) => _processador = processador ?? throw new ArgumentNullException(nameof(processador));

    /// <summary>Recebe uma telemetria assinada da Sentinela.</summary>
    /// <remarks>
    /// O corpo é exatamente o payload do tópico <c>telemetria</c> do contrato MQTT, com o <c>sig</c> HMAC. Assinatura
    /// errada dá 401; <c>seq</c> repetido ou <c>ts</c> fora da janela de 300 s dá 409 (R10); leitura fora da faixa física
    /// dá 422 (R9). Toda recusa vira um evento de segurança.
    /// </remarks>
    /// <param name="deviceId" example="snt-7f3a9c">A Sentinela.</param>
    /// <param name="telemetria">O payload assinado.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>202 quando a leitura entrou.</returns>
    [HttpPost("{deviceId}/leituras")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Receber(string deviceId, JsonElement telemetria, CancellationToken cancellationToken)
    {
        ResultadoDaMensagem resultado = await _processador.ProcessarAsync(new MensagemDaSentinela(deviceId, "telemetria", telemetria.GetRawText()), cancellationToken);
        return resultado switch
        {
            ResultadoDaMensagem.Aceita => Accepted(),
            ResultadoDaMensagem.Invalida => Recusa(StatusCodes.Status400BadRequest, "Telemetria fora do contrato."),
            ResultadoDaMensagem.Desconhecida => Recusa(StatusCodes.Status404NotFound, "Sentinela não vinculada a nenhuma casa."),
            ResultadoDaMensagem.Repetida or ResultadoDaMensagem.ForaDaJanela => Recusa(StatusCodes.Status409Conflict, "Mensagem repetida ou fora da janela de tempo."),
            ResultadoDaMensagem.LeituraRecusada => Recusa(StatusCodes.Status422UnprocessableEntity, "Leitura fora da faixa física."),
            _ => Recusa(StatusCodes.Status401Unauthorized, "Assinatura da Sentinela inválida."),
        };
    }

    private ObjectResult Recusa(int status, string detalhe) => Problem(statusCode: status, title: "Telemetria recusada", detail: detalhe, type: TradutorDeExcecoes.Tipo);
}
