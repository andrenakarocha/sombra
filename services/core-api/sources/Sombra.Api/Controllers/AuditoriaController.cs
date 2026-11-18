using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Comum;
using Sombra.Infraestrutura.Mqtt;

namespace Sombra.Api.Controllers;

/// <summary>Os dados que os cenários de auditoria mandam (só sintéticos).</summary>
public sealed record CenarioDeAuditoriaDto
{
    /// <summary>Gets um laudo (A2).</summary>
    public string? Laudo { get; init; }

    /// <summary>Gets um Cartão SUS (A4).</summary>
    public string? CartaoSus { get; init; }

    /// <summary>Gets um CPF (A5).</summary>
    public string? Cpf { get; init; }

    /// <summary>Gets um telefone (A6).</summary>
    public string? Telefone { get; init; }
}

/// <summary>
/// Os endpoints <b>propositalmente vulneráveis</b> da auditoria (spec 07 §5): cada um faz o erro clássico de um cenário
/// (logar o Cartão SUS, pôr PII no prompt, publicar PII no MQTT, logar a URL com senha, devolver o laudo na mensagem de
/// erro) para provar que os guardas de sink bloqueiam. Só existem nos ambientes <c>Demo</c> e <c>Test</c>; fora deles,
/// 404. Ficam fora do contrato OpenAPI.
/// </summary>
[ApiController]
[Route("api/v1/auditoria")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed partial class AuditoriaController : ControllerBase
{
    /// <summary>O HttpClient que faz o papel do provedor de LLM no cenário A5 (passa pelo guarda de saída).</summary>
    public const string ClienteLlm = "auditoria-llm";

    private readonly IWebHostEnvironment _ambiente;
    private readonly ILogger<AuditoriaController> _log;

    /// <summary>Initializes a new instance of the <see cref="AuditoriaController"/> class.</summary>
    /// <param name="ambiente">O ambiente (só Demo e Test).</param>
    /// <param name="log">O log.</param>
    public AuditoriaController(IWebHostEnvironment ambiente, ILogger<AuditoriaController> log)
    {
        _ambiente = ambiente ?? throw new ArgumentNullException(nameof(ambiente));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    private bool Disponivel => _ambiente.IsEnvironment("Demo") || _ambiente.IsEnvironment("Test");

    /// <summary>A2: a mensagem de erro carrega o laudo do payload.</summary>
    /// <param name="dto">O cenário.</param>
    /// <returns>Nunca volta bem: a regra é violada com o laudo na mensagem.</returns>
    [HttpPost("excecao")]
    public IActionResult Excecao(CenarioDeAuditoriaDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        // nosemgrep: sombra-pii-na-mensagem-de-erro -- vulnerabilidade proposital da auditoria (A2); o Sombra.Privacidade bloqueia em execução
        return Disponivel ? throw new RegraVioladaException("A2", $"Laudo recusado: {dto.Laudo}") : NotFound();
    }

    /// <summary>Resposta não autenticada que devolve a entrada (o eco esquecido de um endpoint de teste).</summary>
    /// <param name="dto">O cenário.</param>
    /// <returns>O próprio cenário, com o laudo.</returns>
    [HttpPost("eco")]
    public IActionResult Eco(CenarioDeAuditoriaDto dto) => Disponivel ? Ok(dto) : NotFound();

    /// <summary>A3: o perfil de saúde de um morador, sem login (o endpoint de teste esquecido aberto).</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="moradorId">O morador.</param>
    /// <param name="casas">As casas.</param>
    /// <param name="cancellationToken">Cancela.</param>
    /// <returns>O perfil, em claro.</returns>
    [HttpGet("perfil/{domicilioId:guid}/{moradorId:guid}")]
    public async Task<IActionResult> Perfil(Guid domicilioId, Guid moradorId, [FromServices] IDomicilioRepository casas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(casas);
        if (!Disponivel || (await casas.ObterAsync(domicilioId, cancellationToken))?.Moradores.FirstOrDefault(m => m.Id == moradorId) is not { } morador)
        {
            return NotFound();
        }

        return Ok(new { nome = morador.Nome, cpf = morador.Cpf.Valor, observacoesDeSaude = morador.ObservacoesDeSaude });
    }

    /// <summary>O "provedor de LLM" do cenário A5: devolve o que recebeu, para a auditoria ver se o dado chegou lá.</summary>
    /// <param name="corpo">O pedido recebido.</param>
    /// <returns>O eco.</returns>
    [HttpPost("receptor/v1/gerar")]
    public IActionResult Receptor([FromBody] JsonElement corpo) => Disponivel ? Ok(new { recebido = corpo }) : NotFound();

    /// <summary>A4: o "log de depuração" escreve o Cartão SUS.</summary>
    /// <param name="dto">O cenário.</param>
    /// <returns>204.</returns>
    [HttpPost("log")]
    public IActionResult Log(CenarioDeAuditoriaDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (!Disponivel)
        {
            return NotFound();
        }

        // nosemgrep: sombra-pii-para-log -- vulnerabilidade proposital da auditoria (A4); o Sombra.Privacidade bloqueia em execução
        CartaoDeDepuracao(_log, dto.CartaoSus);
        return NoContent();
    }

    /// <summary>A5: o prompt da IA leva o CPF.</summary>
    /// <param name="dto">O cenário.</param>
    /// <param name="fabrica">Os HttpClients.</param>
    /// <param name="cancellationToken">Cancela.</param>
    /// <returns>202 se o provedor aceitou (só sem PII).</returns>
    [HttpPost("llm")]
    public async Task<IActionResult> Llm(CenarioDeAuditoriaDto dto, [FromServices] IHttpClientFactory fabrica, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(fabrica);
        if (!Disponivel)
        {
            return NotFound();
        }

        // nosemgrep: sombra-pii-para-fora -- vulnerabilidade proposital da auditoria (A5); o Sombra.Privacidade bloqueia em execução
        using HttpResponseMessage resposta = await fabrica.CreateClient(ClienteLlm)
            .PostAsJsonAsync(new Uri("v1/gerar", UriKind.Relative), new { prompt = $"Explique o risco do cidadão de CPF {dto.Cpf}." }, cancellationToken);
        return new ContentResult
        {
            StatusCode = (int)resposta.StatusCode,
            Content = await resposta.Content.ReadAsStringAsync(cancellationToken),
            ContentType = "application/json",
        };
    }

    /// <summary>A6: a publicação MQTT leva o telefone.</summary>
    /// <param name="dto">O cenário.</param>
    /// <param name="mqtt">A conexão com o broker.</param>
    /// <param name="cancellationToken">Cancela.</param>
    /// <returns>202.</returns>
    [HttpPost("mqtt")]
    public async Task<IActionResult> Mqtt(CenarioDeAuditoriaDto dto, [FromServices] ConexaoMqtt mqtt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(mqtt);
        if (!Disponivel)
        {
            return NotFound();
        }

        // nosemgrep: sombra-pii-para-fora -- vulnerabilidade proposital da auditoria (A6); o Sombra.Privacidade bloqueia em execução
        await mqtt.PublicarAsync($"{mqtt.Prefixo}/auditoria/eco", JsonSerializer.Serialize(new { aviso = $"Ligar para {dto.Telefone}" }), cancellationToken);
        return Accepted();
    }

    /// <summary>A7: o login pela URL, com a senha na query string, e a URL inteira no log.</summary>
    /// <param name="usuario">O usuário.</param>
    /// <param name="senha">A senha (na URL, o erro do cenário).</param>
    /// <returns>200.</returns>
    [HttpGet("login")]
    public IActionResult LoginPelaUrl([FromQuery] string? usuario, [FromQuery] string? senha)
    {
        if (!Disponivel)
        {
            return NotFound();
        }

        string url = $"{Request.Path}{Request.QueryString}";
        LoginPelaUrl(_log, url);
        return Ok(new { recebido = usuario is not null && senha is not null });
    }

    [LoggerMessage(EventId = 1201, EventName = "auditoria.log_de_depuracao", Level = LogLevel.Information, Message = "Depuração: cartão {cartao}")]
    private static partial void CartaoDeDepuracao(ILogger logger, string? cartao);

    [LoggerMessage(EventId = 1202, EventName = "auditoria.login_pela_url", Level = LogLevel.Information, Message = "Login recebido em {url}")]
    private static partial void LoginPelaUrl(ILogger logger, string url);
}
