using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sombra.Infraestrutura.Mqtt;

namespace Sombra.Api.Composicao;

/// <summary>O Core só está pronto com o broker MQTT conectado (as Sentinelas falam por ele).</summary>
public sealed class VerificacaoDoBroker : IHealthCheck
{
    private readonly ConexaoMqtt _conexao;

    /// <summary>Initializes a new instance of the <see cref="VerificacaoDoBroker"/> class.</summary>
    /// <param name="conexao">A conexão com o broker.</param>
    public VerificacaoDoBroker(ConexaoMqtt conexao) => _conexao = conexao ?? throw new ArgumentNullException(nameof(conexao));

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(_conexao.EstaConectada
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Sem conexão com {_conexao.Host}:{_conexao.Porta}"));
}
