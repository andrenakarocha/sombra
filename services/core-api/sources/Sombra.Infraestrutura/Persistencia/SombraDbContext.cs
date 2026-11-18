using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Autenticacao;
using Sombra.Infraestrutura.Persistencia.Configuracoes;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>O banco do Core (PostgreSQL, ADR-003), que também é a unidade de trabalho dos casos de uso.</summary>
public sealed class SombraDbContext : DbContext, IUnitOfWork
{
    private readonly ILogger _log;

    /// <summary>Initializes a new instance of the <see cref="SombraDbContext"/> class, sem log (migrações e testes).</summary>
    /// <param name="options">As opções, montadas por <see cref="Configurar"/>.</param>
    public SombraDbContext(DbContextOptions<SombraDbContext> options)
        : this(options, NullLogger<SombraDbContext>.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SombraDbContext"/> class.</summary>
    /// <param name="options">As opções, montadas por <see cref="Configurar"/>.</param>
    /// <param name="log">O log dos fatos de negócio gravados.</param>
    public SombraDbContext(DbContextOptions<SombraDbContext> options, ILogger<SombraDbContext> log)
        : base(options)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Gets as casas, com moradores e Sentinelas.</summary>
    public DbSet<Domicilio> Domicilios => Set<Domicilio>();

    /// <summary>Gets as leituras das Sentinelas.</summary>
    public DbSet<Leitura> Leituras => Set<Leitura>();

    /// <summary>Gets os alertas, de todos os tipos.</summary>
    public DbSet<AlertaDeCalor> Alertas => Set<AlertaDeCalor>();

    /// <summary>Gets os check-ins.</summary>
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    /// <summary>Gets as contas de acesso.</summary>
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    /// <summary>Monta as opções do contexto: Npgsql e nomes em snake_case (os mesmos no app e nas migrações).</summary>
    /// <param name="builder">O construtor de opções.</param>
    /// <param name="conexao">A string de conexão.</param>
    /// <returns>O mesmo construtor.</returns>
    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder builder, string conexao)
        => builder.UseNpgsql(conexao, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)).UseSnakeCaseNamingConvention();

    /// <summary>
    /// Grava tudo numa transação e só então registra no log os fatos de negócio: leituras e check-ins novos e os eventos
    /// de domínio dos alertas, que são esquecidos depois de registrados.
    /// </summary>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    public async Task SalvarAsync(CancellationToken cancellationToken)
    {
        List<(Leitura Leitura, Guid DomicilioId)> leituras = [.. ChangeTracker.Entries<Leitura>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity, e.Property<Guid>(LeituraConfiguration.DomicilioId).CurrentValue))];
        List<CheckIn> checkIns = [.. ChangeTracker.Entries<CheckIn>().Where(e => e.State == EntityState.Added).Select(e => e.Entity)];
        List<RaizDeAgregado> agregados = [.. ChangeTracker.Entries<RaizDeAgregado>().Select(e => e.Entity).Where(a => a.EventosDeDominio.Count > 0)];

        await SaveChangesAsync(cancellationToken);

        foreach ((Leitura leitura, Guid domicilioId) in leituras)
        {
            _log.LeituraRegistrada(leitura.DeviceId, domicilioId, leitura.Nivel);
        }

        foreach (CheckIn checkIn in checkIns)
        {
            _log.CheckInRegistrado(checkIn.Situacao, checkIn.Origem, checkIn.DomicilioId);
            if (checkIn.Situacao == SituacaoDoCheckIn.PrecisoDeAjuda)
            {
                _log.PedidoDeAjudaRecebido(checkIn.DomicilioId, checkIn.Origem);
            }
        }

        foreach (RaizDeAgregado agregado in agregados)
        {
            foreach (EventoDeDominio evento in agregado.EventosDeDominio)
            {
                RegistrarNoLog(evento);
            }

            agregado.LimparEventos();
        }
    }

    private void RegistrarNoLog(EventoDeDominio evento)
    {
        switch (evento)
        {
            case AlertaAberto a:
                _log.AlertaAberto(a.AlertaId, a.Tipo, a.DomicilioId, a.Nivel);
                break;
            case AlertaAgravado a:
                _log.AlertaAgravado(a.AlertaId, a.DomicilioId, a.Nivel);
                break;
            case AlertaEscalonado a:
                _log.AlertaEscalonado(a.AlertaId, a.DomicilioId, (int)a.Degrau);
                break;
            case AlertaReconhecido a:
                _log.AlertaReconhecido(a.AlertaId, a.DomicilioId, a.Acao);
                break;
            case AlertaEncerrado { Estado: EstadoDoAlerta.Expirado } a:
                _log.AlertaExpirado(a.AlertaId, a.DomicilioId);
                break;
            case AlertaEncerrado a:
                double minutos = Math.Round(a.MinutosAteResolver, 1);
                _log.AlertaResolvido(a.AlertaId, a.DomicilioId, minutos);
                break;
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SombraDbContext).Assembly);
}
