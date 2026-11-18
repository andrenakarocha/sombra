using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Comum;

namespace Sombra.Api.Composicao;

/// <summary>
/// Marca uma escrita que o app pode reenviar da fila offline (F6.23): com o cabeçalho <c>Idempotency-Key</c>, a mesma
/// chave devolve a mesma resposta por 24 horas, sem repetir o efeito.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class IdempotenteAttribute : TypeFilterAttribute
{
    /// <summary>Initializes a new instance of the <see cref="IdempotenteAttribute"/> class.</summary>
    public IdempotenteAttribute()
        : base(typeof(FiltroDeIdempotencia))
    {
    }
}

/// <summary>
/// O filtro do <see cref="IdempotenteAttribute"/>. Reserva a chave antes de executar, guarda a resposta de sucesso e
/// solta a chave quando a requisição falha, para a próxima tentativa executar de novo.
/// </summary>
public sealed partial class FiltroDeIdempotencia : IAsyncActionFilter
{
    /// <summary>O cabeçalho que o cliente manda.</summary>
    public const string Cabecalho = "Idempotency-Key";

    /// <summary>O cabeçalho que marca uma resposta repetida.</summary>
    public const string CabecalhoDeRepeticao = "Idempotency-Replayed";

    private readonly IRegistroDeIdempotencia _registro;
    private readonly JsonSerializerOptions _json;

    /// <summary>Initializes a new instance of the <see cref="FiltroDeIdempotencia"/> class.</summary>
    /// <param name="registro">Onde as respostas ficam guardadas.</param>
    /// <param name="json">O JSON dos controllers, para guardar a resposta igual à que saiu.</param>
    public FiltroDeIdempotencia(IRegistroDeIdempotencia registro, IOptions<JsonOptions> json)
    {
        ArgumentNullException.ThrowIfNull(json);
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        _json = json.Value.JsonSerializerOptions;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        HttpContext http = context.HttpContext;
        string? chave = http.Request.Headers[Cabecalho].FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(chave))
        {
            await next();
            return;
        }

        if (!ChaveValida().IsMatch(chave))
        {
            throw new ValidacaoException(new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [Cabecalho] = ["Use até 100 letras, números, hífen, sublinhado, dois-pontos ou ponto."],
            });
        }

        CancellationToken cancellationToken = http.RequestAborted;
        Guid usuario = http.User.Id();
        string rota = $"{http.Request.Method} {http.Request.Path}";
        RespostaGuardada? anterior = await _registro.ObterAsync(usuario, chave, cancellationToken);
        if (anterior is null && !await _registro.ReservarAsync(usuario, chave, rota, cancellationToken))
        {
            anterior = await _registro.ObterAsync(usuario, chave, cancellationToken)
                ?? throw new ConflitoException("Uma requisição com esta Idempotency-Key ainda está em andamento.");
        }

        if (anterior is not null)
        {
            context.Result = Repetir(anterior, rota, http);
            return;
        }

        ActionExecutedContext executado = await next();
        if (executado.Exception is null && executado.Result is ObjectResult { Value: { } valor } resultado && resultado.StatusCode is null or (>= 200 and < 300))
        {
            await _registro.ConcluirAsync(usuario, chave, resultado.StatusCode ?? StatusCodes.Status200OK, JsonSerializer.Serialize(valor, valor.GetType(), _json), CancellationToken.None);
            return;
        }

        await _registro.LiberarAsync(usuario, chave, CancellationToken.None);
    }

    private static ContentResult Repetir(RespostaGuardada anterior, string rota, HttpContext http)
    {
        if (!string.Equals(anterior.Rota, rota, StringComparison.Ordinal))
        {
            throw new RegraVioladaException("IDEMPOTENCIA", "Esta Idempotency-Key já foi usada em outra operação.");
        }

        if (anterior.Status is not { } status)
        {
            throw new ConflitoException("Uma requisição com esta Idempotency-Key ainda está em andamento.");
        }

        http.Response.Headers[CabecalhoDeRepeticao] = "true";
        return new ContentResult { StatusCode = status, Content = anterior.Corpo, ContentType = "application/json; charset=utf-8" };
    }

    [GeneratedRegex("^[A-Za-z0-9._:-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChaveValida();
}
