using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sombra.Api.Autenticacao;
using Sombra.Api.Composicao;
using Sombra.Api.Erros;
using Sombra.Aplicacao.Dtos;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.Controllers;

/// <summary>Login e emissão do token.</summary>
[ApiController]
[Route("api/v1/auth")]
[Tags("Autenticação")]
public sealed class AutenticacaoController : ControllerBase
{
    private readonly ContasDeAcesso _contas;
    private readonly EmissorDeToken _emissor;

    /// <summary>Initializes a new instance of the <see cref="AutenticacaoController"/> class.</summary>
    /// <param name="contas">As contas de acesso.</param>
    /// <param name="emissor">O emissor de tokens.</param>
    public AutenticacaoController(ContasDeAcesso contas, EmissorDeToken emissor)
    {
        _contas = contas ?? throw new ArgumentNullException(nameof(contas));
        _emissor = emissor ?? throw new ArgumentNullException(nameof(emissor));
    }

    /// <summary>Entra e recebe o token.</summary>
    /// <remarks>O token vale 8 horas. O login tem um limite próprio de tentativas por minuto, contra força bruta.</remarks>
    /// <param name="dto">E-mail e senha.</param>
    /// <param name="cancellationToken">Cancela a requisição.</param>
    /// <returns>O token, o papel e quando vence.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ComposicaoDoCore.LimiteDoLogin)]
    [ProducesResponseType<TokenResposta>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<TokenResposta>> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Usuario? usuario = await _contas.AutenticarAsync(dto.Email, dto.Senha, cancellationToken);
        return usuario is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Não autenticado", detail: "E-mail ou senha não conferem.", type: TradutorDeExcecoes.Tipo)
            : _emissor.Emitir(usuario);
    }
}
