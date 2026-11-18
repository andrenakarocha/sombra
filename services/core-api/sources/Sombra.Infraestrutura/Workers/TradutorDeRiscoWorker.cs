using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sombra.Infraestrutura.Ia;

namespace Sombra.Infraestrutura.Workers;

/// <summary>Traduz os eventos de segurança pendentes a cada volta (<c>Workers:TradutorSegundos</c>, padrão 15 s).</summary>
public sealed class TradutorDeRiscoWorker : WorkerPeriodico
{
    /// <summary>Initializes a new instance of the <see cref="TradutorDeRiscoWorker"/> class.</summary>
    /// <param name="escopos">Para abrir um escopo por volta.</param>
    /// <param name="configuracao">A configuração.</param>
    /// <param name="log">O log.</param>
    public TradutorDeRiscoWorker(IServiceScopeFactory escopos, IConfiguration configuracao, ILogger<TradutorDeRiscoWorker> log)
        : base(escopos, log, TimeSpan.FromSeconds(configuracao.GetValue("Workers:TradutorSegundos", 15.0)), "tradutor-de-risco")
    {
    }

    /// <inheritdoc/>
    protected override Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken)
        => servicos.GetRequiredService<TradutorDeRisco>().TraduzirPendentesAsync(cancellationToken);
}
