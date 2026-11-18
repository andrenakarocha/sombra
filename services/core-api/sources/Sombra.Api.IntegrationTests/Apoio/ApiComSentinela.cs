using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Calor;

namespace Sombra.Api.IntegrationTests.Apoio;

/// <summary>Um Clima Service de teste: a previsão muda quando o roteiro manda.</summary>
internal sealed class ClimaDeTeste : IClimaGateway
{
    /// <summary>Gets or sets o nível previsto para as próximas 24 h, ou nulo para o Clima fora do ar.</summary>
    internal NivelDeRisco? Previsto { get; set; }

    /// <inheritdoc/>
    public Task<RiscoDaRegiao?> ObterRiscoAsync(string regiaoClimatica, CancellationToken cancellationToken)
        => Task.FromResult(Previsto is { } nivel ? new RiscoDaRegiao(NivelDeRisco.Atencao, nivel, 0.83, "teste") : null);
}

/// <summary>
/// A API em teste com a Sentinela da casa da Dona Lurdes (<c>snt-7f3a9c</c>) já com segredo, workers rápidos, o relógio
/// controlado pelo teste e o Clima de teste.
/// </summary>
internal static class ApiComSentinela
{
    /// <summary>A Sentinela da demonstração.</summary>
    internal const string Sentinela = "snt-7f3a9c";

    /// <summary>Gets o segredo da Sentinela nesta execução.</summary>
    internal static string Segredo { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    /// <summary>Gets o token do monitor da VM nesta execução.</summary>
    internal static string TokenDoMonitor { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    /// <summary>Monta a API.</summary>
    /// <param name="relogio">O relógio do teste.</param>
    /// <param name="clima">O Clima de teste (sem previsão, por padrão).</param>
    /// <returns>A fábrica.</returns>
    internal static FabricaDaApi Nova(RelogioDeTeste relogio, ClimaDeTeste? clima = null) => new(
        configuracao: new Dictionary<string, string>
        {
            ["Demo:SentinelaDeviceId"] = Sentinela,
            ["Demo:SentinelaSegredoHex"] = Segredo,
            ["Workers:EscalonadorSegundos"] = "0.3",
            ["Workers:OutboxSegundos"] = "0.2",
            ["Workers:PrevisaoMinutos"] = "0.005",
            ["Ops:TokenDoMonitor"] = TokenDoMonitor,
        },
        servicos: servicos =>
        {
            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(relogio);
            servicos.RemoveAll<IClimaGateway>();
            servicos.AddSingleton<IClimaGateway>(clima ?? new ClimaDeTeste());
        });

    /// <summary>O JWT de uma conta da demo.</summary>
    /// <param name="api">A API.</param>
    /// <param name="email">A conta.</param>
    /// <returns>O token.</returns>
    internal static async Task<string> TokenAsync(this FabricaDaApi api, string email)
    {
        using HttpClient cliente = await api.ClienteDeAsync(email);
        return cliente.DefaultRequestHeaders.Authorization!.Parameter!;
    }
}
