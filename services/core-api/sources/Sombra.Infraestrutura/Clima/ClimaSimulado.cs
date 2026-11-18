using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Calor;

namespace Sombra.Infraestrutura.Clima;

/// <summary>
/// O stub do contrato do Clima até a F3.9: a previsão de cada região vem da configuração (ex.:
/// <c>Clima__Simulado__SP-MIRANTE=PERIGO</c>). Sem configuração, responde como o Clima fora do ar (nulo).
/// </summary>
public sealed class ClimaSimulado : IClimaGateway
{
    private readonly IConfiguration _configuracao;

    /// <summary>Initializes a new instance of the <see cref="ClimaSimulado"/> class.</summary>
    /// <param name="configuracao">A configuração.</param>
    public ClimaSimulado(IConfiguration configuracao) => _configuracao = configuracao ?? throw new ArgumentNullException(nameof(configuracao));

    /// <inheritdoc/>
    public Task<RiscoDaRegiao?> ObterRiscoAsync(string regiaoClimatica, CancellationToken cancellationToken)
    {
        string texto = (_configuracao[$"Clima:Simulado:{regiaoClimatica}"] ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal);
        RiscoDaRegiao? risco = Enum.TryParse(texto, ignoreCase: true, out NivelDeRisco nivel) && Enum.IsDefined(nivel)
            ? new RiscoDaRegiao(nivel, nivel, nivel >= NivelDeRisco.Perigo ? 0.9 : 0.1, "simulado")
            : null;
        return Task.FromResult(risco);
    }
}
