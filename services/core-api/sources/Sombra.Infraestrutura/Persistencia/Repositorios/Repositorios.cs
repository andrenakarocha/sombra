using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia.Configuracoes;

namespace Sombra.Infraestrutura.Persistencia.Repositorios;

/// <summary>As casas no PostgreSQL. Moradores e Sentinelas vêm junto (auto include); a rede vem na própria linha.</summary>
public sealed class DomicilioRepository : IDomicilioRepository
{
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="DomicilioRepository"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public DomicilioRepository(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public Task<Domicilio?> ObterAsync(Guid id, CancellationToken cancellationToken)
        => _db.Domicilios.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc/>
    public Task<Domicilio?> ObterPorSentinelaAsync(string deviceId, CancellationToken cancellationToken)
        => _db.Domicilios.SingleOrDefaultAsync(d => d.Sentinelas.Any(s => s.DeviceId == deviceId), cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Domicilio>> ListarMonitoradosAsync(CancellationToken cancellationToken)
        => await _db.Domicilios.Where(d => d.Monitorado).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public void Adicionar(Domicilio domicilio) => _db.Domicilios.Add(domicilio);

    /// <inheritdoc/>
    public void Remover(Domicilio domicilio) => _db.Domicilios.Remove(domicilio);
}

/// <summary>Os alertas no PostgreSQL (uma tabela para os três tipos).</summary>
public sealed class AlertaRepository : IAlertaRepository
{
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="AlertaRepository"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public AlertaRepository(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public Task<AlertaDeCalor?> ObterAsync(Guid id, CancellationToken cancellationToken)
        => _db.Alertas.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosDaCasaAsync(Guid domicilioId, CancellationToken cancellationToken)
        => await Abertos().Where(a => a.DomicilioId == domicilioId).OrderBy(a => a.AbertoEm).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<AlertaDeCalor?> ObterUltimoEncerradoAsync(Guid domicilioId, CancellationToken cancellationToken)
        => _db.Alertas
            .Where(a => a.DomicilioId == domicilioId && a.EncerradoEm != null)
            .OrderByDescending(a => a.EncerradoEm)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosAsync(CancellationToken cancellationToken)
        => await Abertos().OrderBy(a => a.AbertoEm).ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public void Adicionar(AlertaDeCalor alerta) => _db.Alertas.Add(alerta);

    private IQueryable<AlertaDeCalor> Abertos()
        => _db.Alertas.Where(a => a.Estado != EstadoDoAlerta.Resolvido && a.Estado != EstadoDoAlerta.Expirado);
}

/// <summary>As leituras no PostgreSQL, ligadas à casa pela chave de sombra.</summary>
public sealed class LeituraRepository : ILeituraRepository
{
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="LeituraRepository"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public LeituraRepository(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public void Adicionar(Guid domicilioId, Leitura leitura)
        => _db.Leituras.Add(leitura).Property(LeituraConfiguration.DomicilioId).CurrentValue = domicilioId;

    /// <inheritdoc/>
    public Task<Leitura?> ObterUltimaAsync(Guid domicilioId, CancellationToken cancellationToken)
        => _db.Leituras
            .Where(l => EF.Property<Guid>(l, LeituraConfiguration.DomicilioId) == domicilioId)
            .OrderByDescending(l => l.Momento)
            .FirstOrDefaultAsync(cancellationToken);
}

/// <summary>Os check-ins no PostgreSQL.</summary>
public sealed class CheckInRepository : ICheckInRepository
{
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="CheckInRepository"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public CheckInRepository(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public void Adicionar(CheckIn checkIn) => _db.CheckIns.Add(checkIn);

    /// <inheritdoc/>
    public Task<CheckIn?> ObterUltimoAsync(Guid domicilioId, CancellationToken cancellationToken)
        => _db.CheckIns.Where(c => c.DomicilioId == domicilioId).OrderByDescending(c => c.Momento).FirstOrDefaultAsync(cancellationToken);
}
