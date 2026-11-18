using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia.Configuracoes;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Privacidade.Risco;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// As consultas do app e do painel em LINQ: projeções direto para a resposta, <c>AsNoTracking</c>, filtros, paginação
/// e agregações com <c>GroupBy</c> traduzidas para SQL pelo EF Core.
/// </summary>
public sealed class ConsultasDoPainel : IConsultasDoPainel
{
    private static readonly TimeSpan _toleranciaOnline = Sentinela.IntervaloDeTelemetria * Sentinela.TelemetriasPerdidasParaOffline;
    private readonly SombraDbContext _db;

    /// <summary>Initializes a new instance of the <see cref="ConsultasDoPainel"/> class.</summary>
    /// <param name="db">O contexto da requisição.</param>
    public ConsultasDoPainel(SombraDbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <inheritdoc/>
    public async Task<Paginado<DomicilioDetalheResposta>> ListarDomiciliosAsync(FiltroDeDomiciliosDto filtro, IReadOnlyCollection<Guid>? soEstasCasas, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        IQueryable<Domicilio> visiveis = soEstasCasas is null ? _db.Domicilios : _db.Domicilios.Where(d => soEstasCasas.Contains(d.Id));
        var casas = visiveis.AsNoTracking().Select(d => new
        {
            d.Id,
            d.Apelido,
            d.Endereco.Bairro,
            Nivel = _db.Leituras
                .Where(l => EF.Property<Guid>(l, LeituraConfiguration.DomicilioId) == d.Id)
                .OrderByDescending(l => l.Momento)
                .Select(l => (NivelDeRisco?)l.Nivel)
                .FirstOrDefault() ?? NivelDeRisco.Normal,
        });

        if (!string.IsNullOrWhiteSpace(filtro.Bairro))
        {
            string bairro = filtro.Bairro.Trim();
            casas = casas.Where(c => c.Bairro == bairro);
        }

        if (filtro.Nivel is { } nivel)
        {
            casas = casas.Where(c => c.Nivel == nivel);
        }

        int total = await casas.CountAsync(cancellationToken);
        List<Guid> ids = await casas
            .OrderBy(c => c.Apelido)
            .ThenBy(c => c.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // O card da Início mostra o mesmo que a tela da casa (D17): a página inteira sai em quatro consultas, não quatro por casa.
        Dictionary<Guid, Domicilio> doPagina = await _db.Domicilios.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);
        Dictionary<Guid, Leitura> ultimasLeituras = (await _db.Leituras.AsNoTracking()
                .Where(l => ids.Contains(EF.Property<Guid>(l, LeituraConfiguration.DomicilioId)))
                .GroupBy(l => EF.Property<Guid>(l, LeituraConfiguration.DomicilioId))
                .Select(g => new { DomicilioId = g.Key, Leitura = g.OrderByDescending(l => l.Momento).First() })
                .ToListAsync(cancellationToken))
            .ToDictionary(u => u.DomicilioId, u => u.Leitura);
        Dictionary<Guid, DateTimeOffset> ultimosCheckIns = await _db.CheckIns.AsNoTracking()
            .Where(c => ids.Contains(c.DomicilioId))
            .GroupBy(c => c.DomicilioId)
            .Select(g => new { DomicilioId = g.Key, Momento = g.Max(c => c.Momento) })
            .ToDictionaryAsync(u => u.DomicilioId, u => u.Momento, cancellationToken);
        ILookup<Guid, AlertaDeCalor> abertos = (await _db.Alertas.AsNoTracking()
                .Where(a => ids.Contains(a.DomicilioId) && a.Estado != EstadoDoAlerta.Resolvido && a.Estado != EstadoDoAlerta.Expirado)
                .OrderBy(a => a.AbertoEm)
                .ToListAsync(cancellationToken))
            .ToLookup(a => a.DomicilioId);

        return new(
            [.. ids.Select(id => DomicilioDetalheResposta.De(
                doPagina[id],
                ultimasLeituras.GetValueOrDefault(id),
                ultimosCheckIns.TryGetValue(id, out DateTimeOffset checkIn) ? checkIn : null,
                DomicilioDetalheResposta.AlertaQueAparece([.. abertos[id]]),
                agora))],
            filtro.Pagina,
            filtro.Tamanho,
            total);
    }

    /// <inheritdoc/>
    public async Task<Paginado<AlertaResposta>> ListarAlertasAsync(FiltroDeAlertasDto filtro, IReadOnlyCollection<Guid>? soEstasCasas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        IQueryable<AlertaDeCalor> alertas = _db.Alertas.AsNoTracking();
        if (soEstasCasas is not null)
        {
            alertas = alertas.Where(a => soEstasCasas.Contains(a.DomicilioId));
        }

        if (filtro.Estado is { } estado)
        {
            IReadOnlyList<EstadoDoAlerta> doDominio = AlertaResposta.EstadosDoDominio(estado);
            alertas = alertas.Where(a => doDominio.Contains(a.Estado));
        }

        if (filtro.Nivel is { } nivel)
        {
            alertas = alertas.Where(a => a.Nivel == nivel);
        }

        int total = await alertas.CountAsync(cancellationToken);
        List<AlertaDeCalor> pagina = await alertas
            .OrderByDescending(a => a.AbertoEm)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .ToListAsync(cancellationToken);
        Dictionary<Guid, string> apelidos = await ApelidosAsync(pagina.Select(a => a.DomicilioId), cancellationToken);
        return new([.. pagina.Select(a => AlertaResposta.De(a, apelidos.GetValueOrDefault(a.DomicilioId, string.Empty)))], filtro.Pagina, filtro.Tamanho, total);
    }

    /// <inheritdoc/>
    public async Task<AlertaResposta?> ObterAlertaAsync(Guid id, CancellationToken cancellationToken)
    {
        AlertaDeCalor? alerta = await _db.Alertas.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (alerta is null)
        {
            return null;
        }

        Dictionary<Guid, string> apelidos = await ApelidosAsync([alerta.DomicilioId], cancellationToken);
        return AlertaResposta.De(alerta, apelidos.GetValueOrDefault(alerta.DomicilioId, string.Empty));
    }

    /// <inheritdoc/>
    public async Task<PrivacidadeDaCasaResposta> PrivacidadeDaCasaAsync(Guid domicilioId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        string[] sentinelas = await _db.Domicilios.AsNoTracking()
            .Where(d => d.Id == domicilioId)
            .SelectMany(d => d.Sentinelas.Select(s => s.DeviceId))
            .ToArrayAsync(cancellationToken);
        DateTimeOffset desde = agora - PrivacidadeDaCasaResposta.Janela;
        List<EventoDeSeguranca> eventos = await _db.EventosDeSeguranca.AsNoTracking()
            .Where(e => (e.DomicilioId == domicilioId || sentinelas.Contains(e.Origem)) && e.Momento >= desde)
            .ToListAsync(cancellationToken);
        return PrivacidadeDaCasaResposta.De(eventos.Select(e => (e.Tipo, e.ParaRisco())), agora);
    }

    /// <inheritdoc/>
    public async Task<RiscoCidadaoResposta> RiscoCidadaoAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        DateTimeOffset desde = agora - RiscoCidadao.Janela;
        List<EventoDeSeguranca> eventos = await _db.EventosDeSeguranca.AsNoTracking().Where(e => e.Momento >= desde).ToListAsync(cancellationToken);
        return RiscoCidadaoResposta.De([.. eventos.Select(e => (e.ParaRisco(), e.Sink, e.Regra))], agora);
    }

    /// <inheritdoc/>
    public async Task<Paginado<EventoDeSegurancaResposta>> ListarEventosDeSegurancaAsync(ConsultaPaginadaDto filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        int total = await _db.EventosDeSeguranca.CountAsync(cancellationToken);
        List<EventoDeSeguranca> pagina = await _db.EventosDeSeguranca.AsNoTracking()
            .OrderByDescending(e => e.Momento).ThenByDescending(e => e.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .ToListAsync(cancellationToken);
        return new(
            [.. pagina.Select(e => new EventoDeSegurancaResposta(e.Id, e.Momento, e.Tipo, e.Origem, e.Sink, e.Categoria, e.Severidade, e.Regra, e.Bloqueado, e.DomicilioId, e.CorrelationId, e.Detalhe))],
            filtro.Pagina,
            filtro.Tamanho,
            total);
    }

    /// <inheritdoc/>
    public async Task<Paginado<LeituraResposta>> ListarLeiturasAsync(Guid domicilioId, FiltroDeLeiturasDto filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        DateTimeOffset de = filtro.De.GetValueOrDefault().ToUniversalTime();
        DateTimeOffset ate = filtro.Ate.GetValueOrDefault().ToUniversalTime();
        IQueryable<Leitura> leituras = _db.Leituras.AsNoTracking()
            .Where(l => EF.Property<Guid>(l, LeituraConfiguration.DomicilioId) == domicilioId && l.Momento >= de && l.Momento < ate);

        int total = await leituras.CountAsync(cancellationToken);
        List<Leitura> pagina = await leituras
            .OrderByDescending(l => l.Momento)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .ToListAsync(cancellationToken);
        return new([.. pagina.Select(LeituraResposta.De)], filtro.Pagina, filtro.Tamanho, total);
    }

    /// <inheritdoc/>
    public async Task<PainelResumoResposta> ObterResumoAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        DateTimeOffset utc = agora.ToUniversalTime();
        DateTimeOffset semana = utc.AddDays(-7);
        DateTimeOffset hoje = HorarioDeBrasilia.InicioDoDia(utc);
        DateTimeOffset onlineDesde = utc - _toleranciaOnline;

        int casasMonitoradas = await _db.Domicilios.CountAsync(d => d.Monitorado, cancellationToken);

        Dictionary<NivelDeRisco, int> ativosPorNivel = await _db.Alertas
            .Where(a => a.Estado != EstadoDoAlerta.Resolvido && a.Estado != EstadoDoAlerta.Expirado)
            .GroupBy(a => a.Nivel)
            .Select(g => new { Nivel = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Nivel, g => g.Total, cancellationToken);

        var resolvidos = await _db.Alertas.OfType<AlertaDeSentinela>()
            .Where(a => a.Estado == EstadoDoAlerta.Resolvido && a.EncerradoEm >= semana)
            .Select(a => new { a.AbertoEm, a.EncerradoEm, a.Degrau })
            .ToListAsync(cancellationToken);

        int alarmesDesnecessarios = await _db.Alertas.CountAsync(
            a => a.EncerradoEm >= semana && a.Degrau == DegrauDeEscalonamento.AlertaParaARede && a.Reconhecimento == AcaoDoCuidador.ElaEstaBem,
            cancellationToken);

        Dictionary<DegrauDeEscalonamento, int> hojePorDegrau = await _db.Alertas
            .Where(a => a.AbertoEm >= hoje && a.Degrau > DegrauDeEscalonamento.Sentinela)
            .GroupBy(a => a.Degrau)
            .Select(g => new { Degrau = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Degrau, g => g.Total, cancellationToken);

        int sentinelasOffline = await _db.Domicilios
            .Where(d => d.Monitorado)
            .SelectMany(d => d.Sentinelas)
            .CountAsync(s => s.UltimoContato == null || s.UltimoContato < onlineDesde, cancellationToken);

        Dictionary<string, int> porBairro = await _db.Alertas
            .Where(a => a.AbertoEm >= semana)
            .Join(_db.Domicilios, a => a.DomicilioId, d => d.Id, (a, d) => d.Endereco.Bairro)
            .GroupBy(bairro => bairro)
            .Select(g => new { Bairro = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Bairro, g => g.Total, StringComparer.Ordinal, cancellationToken);

        return new PainelResumoResposta(
            casasMonitoradas,
            ativosPorNivel.Values.Sum(),
            Mediana([.. resolvidos.Select(r => (r.EncerradoEm.GetValueOrDefault() - r.AbertoEm).TotalMinutes)]),
            resolvidos.Count == 0 ? null : Math.Round(100.0 * resolvidos.Count(r => r.Degrau < DegrauDeEscalonamento.AlertaParaARede) / resolvidos.Count, 1),
            alarmesDesnecessarios,
            Enumerable.Range(1, 3).ToDictionary(d => d, d => hojePorDegrau.GetValueOrDefault((DegrauDeEscalonamento)d)),
            sentinelasOffline,
            ativosPorNivel,
            porBairro);
    }

    private static double? Mediana(double[] valores)
    {
        if (valores.Length == 0)
        {
            return null;
        }

        Array.Sort(valores);
        int meio = valores.Length / 2;
        double mediana = valores.Length % 2 == 1 ? valores[meio] : (valores[meio - 1] + valores[meio]) / 2.0;
        return Math.Round(mediana, 1);
    }

    private async Task<Dictionary<Guid, string>> ApelidosAsync(IEnumerable<Guid> casas, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. casas.Distinct()];
        return await _db.Domicilios.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, d.Apelido }).ToDictionaryAsync(d => d.Id, d => d.Apelido, cancellationToken);
    }
}
