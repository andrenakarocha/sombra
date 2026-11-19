using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;

namespace Sombra.Infraestrutura.Clima;

/// <summary>
/// Leva o <c>correlationId</c> do trabalho atual (requisição, mensagem MQTT ou volta de worker) para o Clima no header
/// <c>X-Correlation-Id</c>: a mesma chamada aparece nos logs dos dois serviços (observabilidade §3).
/// </summary>
public sealed class CorrelacaoNaSaida : DelegatingHandler
{
    /// <summary>O header de correlação.</summary>
    public const string Header = "X-Correlation-Id";

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Correlacao.Atual is { Length: > 0 } id && !request.Headers.Contains(Header))
        {
            request.Headers.TryAddWithoutValidation(Header, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
