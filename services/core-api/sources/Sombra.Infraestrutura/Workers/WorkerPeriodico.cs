using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Comum;

namespace Sombra.Infraestrutura.Workers;

/// <summary>
/// A base dos workers que trabalham em voltas: cada volta tem um escopo de DI próprio (um <c>DbContext</c> por volta) e
/// uma correlação própria, e uma volta que falha não derruba as seguintes.
/// </summary>
public abstract partial class WorkerPeriodico : BackgroundService
{
    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger _log;
    private readonly TimeSpan _intervalo;
    private readonly string _nome;

    /// <summary>Initializes a new instance of the <see cref="WorkerPeriodico"/> class.</summary>
    /// <param name="escopos">A fábrica de escopos.</param>
    /// <param name="log">O log.</param>
    /// <param name="intervalo">O intervalo entre voltas.</param>
    /// <param name="nome">O nome do worker, para a correlação e o log.</param>
    protected WorkerPeriodico(IServiceScopeFactory escopos, ILogger log, TimeSpan intervalo, string nome)
    {
        _escopos = escopos ?? throw new ArgumentNullException(nameof(escopos));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _intervalo = intervalo;
        _nome = nome;
    }

    /// <summary>Faz uma volta já (os testes chamam direto).</summary>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>A volta.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Uma volta que falha é registrada e a próxima tenta de novo; o worker não pode morrer.")]
    public async Task UmaVoltaAsync(CancellationToken cancellationToken)
    {
        string correlacao = $"{_nome}:{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4))}";
        Correlacao.Atual = correlacao;
        using IDisposable? escopoDoLog = _log.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlacao });
        try
        {
            await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();
            await ExecutarAsync(escopo.ServiceProvider, cancellationToken);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            VoltaFalhou(_log, erro, _nome);
        }
    }

    /// <summary>O trabalho de uma volta.</summary>
    /// <param name="servicos">Os serviços do escopo da volta.</param>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>O trabalho.</returns>
    protected abstract Task ExecutarAsync(IServiceProvider servicos, CancellationToken cancellationToken);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer relogio = new(_intervalo);
        do
        {
            await UmaVoltaAsync(stoppingToken);
        }
        while (await relogio.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(EventId = 4201, EventName = "worker.volta_falhou", Level = LogLevel.Error, Message = "A volta do {worker} falhou; a próxima tenta de novo")]
    private static partial void VoltaFalhou(ILogger logger, Exception excecao, string worker);
}
