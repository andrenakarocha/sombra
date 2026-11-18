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
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia.Configuracoes;

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
    public async Task<Paginado<DomicilioResumoResposta>> ListarDomiciliosAsync(FiltroDeDomiciliosDto filtro, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        DateTimeOffset onlineDesde = agora.ToUniversalTime() - _toleranciaOnline;
        var casas = _db.Domicilios.AsNoTracking().Select(d => new
        {
            d.Id,
            d.Apelido,
            d.Endereco.Bairro,
            d.Monitorado,
            Nivel = _db.Leituras
                .Where(l => EF.Property<Guid>(l, LeituraConfiguration.DomicilioId) == d.Id)
                .OrderByDescending(l => l.Momento)
                .Select(l => (NivelDeRisco?)l.Nivel)
                .FirstOrDefault() ?? NivelDeRisco.Normal,
            Online = d.Sentinelas.Any(s => s.UltimoContato >= onlineDesde),
            AlertaAberto = _db.Alertas.Any(a => a.DomicilioId == d.Id && a.Estado != EstadoDoAlerta.Resolvido && a.Estado != EstadoDoAlerta.Expirado),
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
        var pagina = await casas
            .OrderBy(c => c.Apelido)
            .ThenBy(c => c.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .ToListAsync(cancellationToken);
        return new([.. pagina.Select(c => new DomicilioResumoResposta(c.Id, c.Apelido, c.Bairro, c.Monitorado, c.Nivel, c.Online, c.AlertaAberto))], filtro.Pagina, filtro.Tamanho, total);
    }

    /// <inheritdoc/>
    public async Task<Paginado<AlertaResposta>> ListarAlertasAsync(FiltroDeAlertasDto filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        IQueryable<AlertaDeCalor> alertas = _db.Alertas.AsNoTracking();
        if (filtro.Estado is { } estado)
        {
            alertas = alertas.Where(a => a.Estado == estado);
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
        return new([.. pagina.Select(AlertaResposta.De)], filtro.Pagina, filtro.Tamanho, total);
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
}
