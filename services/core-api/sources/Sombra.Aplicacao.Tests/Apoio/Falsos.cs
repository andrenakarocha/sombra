using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Tests.Apoio;

/// <summary>Um relógio parado, que o teste avança na mão.</summary>
internal sealed class RelogioFixo : IRelogio
{
    /// <summary>Initializes a new instance of the <see cref="RelogioFixo"/> class.</summary>
    /// <param name="agora">O instante inicial.</param>
    internal RelogioFixo(DateTimeOffset agora) => Agora = agora;

    /// <inheritdoc/>
    public DateTimeOffset Agora { get; private set; }

    /// <summary>Anda com o relógio.</summary>
    /// <param name="quanto">Quanto tempo.</param>
    internal void Avancar(TimeSpan quanto) => Agora += quanto;
}

/// <summary>As casas em memória.</summary>
internal sealed class DomiciliosEmMemoria : IDomicilioRepository
{
    /// <summary>Gets as casas guardadas.</summary>
    internal List<Domicilio> Casas { get; } = [];

    /// <inheritdoc/>
    public Task<Domicilio?> ObterAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Casas.SingleOrDefault(c => c.Id == id));

    /// <inheritdoc/>
    public Task<Domicilio?> ObterPorSentinelaAsync(string deviceId, CancellationToken cancellationToken)
        => Task.FromResult(Casas.SingleOrDefault(c => c.Sentinelas.Any(s => s.DeviceId == deviceId)));

    /// <inheritdoc/>
    public Task<IReadOnlyList<Domicilio>> ListarMonitoradosAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Domicilio>>([.. Casas.Where(c => c.Monitorado)]);

    /// <inheritdoc/>
    public void Adicionar(Domicilio domicilio) => Casas.Add(domicilio);

    /// <inheritdoc/>
    public void Remover(Domicilio domicilio) => Casas.Remove(domicilio);
}

/// <summary>Os alertas em memória.</summary>
internal sealed class AlertasEmMemoria : IAlertaRepository
{
    /// <summary>Gets os alertas guardados.</summary>
    internal List<AlertaDeCalor> Alertas { get; } = [];

    /// <inheritdoc/>
    public Task<AlertaDeCalor?> ObterAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Alertas.SingleOrDefault(a => a.Id == id));

    /// <inheritdoc/>
    public Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosDaCasaAsync(Guid domicilioId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AlertaDeCalor>>([.. Alertas.Where(a => a.DomicilioId == domicilioId && a.EstaAberto)]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AlertaDeCalor>>([.. Alertas.Where(a => a.EstaAberto)]);

    /// <inheritdoc/>
    public void Adicionar(AlertaDeCalor alerta) => Alertas.Add(alerta);
}

/// <summary>As leituras em memória.</summary>
internal sealed class LeiturasEmMemoria : ILeituraRepository
{
    /// <summary>Gets as leituras guardadas, com a casa.</summary>
    internal List<(Guid DomicilioId, Leitura Leitura)> Itens { get; } = [];

    /// <inheritdoc/>
    public void Adicionar(Guid domicilioId, Leitura leitura) => Itens.Add((domicilioId, leitura));

    /// <inheritdoc/>
    public Task<Leitura?> ObterUltimaAsync(Guid domicilioId, CancellationToken cancellationToken)
        => Task.FromResult(Itens.Where(i => i.DomicilioId == domicilioId).Select(i => i.Leitura).MaxBy(l => l.Momento));
}

/// <summary>Os check-ins em memória.</summary>
internal sealed class CheckInsEmMemoria : ICheckInRepository
{
    /// <summary>Gets os check-ins guardados.</summary>
    internal List<CheckIn> Itens { get; } = [];

    /// <inheritdoc/>
    public void Adicionar(CheckIn checkIn) => Itens.Add(checkIn);

    /// <inheritdoc/>
    public Task<CheckIn?> ObterUltimoAsync(Guid domicilioId, CancellationToken cancellationToken)
        => Task.FromResult(Itens.Where(c => c.DomicilioId == domicilioId).MaxBy(c => c.Momento));
}

/// <summary>Conta as gravações.</summary>
internal sealed class UnidadeDeTrabalhoContada : IUnitOfWork
{
    /// <summary>Gets quantas vezes o caso de uso gravou.</summary>
    internal int Gravacoes { get; private set; }

    /// <inheritdoc/>
    public Task SalvarAsync(CancellationToken cancellationToken)
    {
        Gravacoes++;
        return Task.CompletedTask;
    }
}

/// <summary>Guarda os avisos em vez de entregar.</summary>
internal sealed class NotificadorGravado : INotificador
{
    /// <summary>Gets os avisos, na ordem em que foram registrados.</summary>
    internal List<Notificacao> Avisos { get; } = [];

    /// <inheritdoc/>
    public Task NotificarAsync(Notificacao notificacao, CancellationToken cancellationToken)
    {
        Avisos.Add(notificacao);
        return Task.CompletedTask;
    }
}

/// <summary>Um Clima Service de mentira, com o risco de cada região definido pelo teste.</summary>
internal sealed class ClimaFalso : IClimaGateway
{
    /// <summary>Gets o risco de cada região; região ausente simula o Clima fora do ar.</summary>
    internal Dictionary<string, RiscoDaRegiao> Riscos { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets quantas consultas chegaram.</summary>
    internal int Consultas { get; private set; }

    /// <inheritdoc/>
    public Task<RiscoDaRegiao?> ObterRiscoAsync(string regiaoClimatica, CancellationToken cancellationToken)
    {
        Consultas++;
        return Task.FromResult(Riscos.GetValueOrDefault(regiaoClimatica));
    }
}
