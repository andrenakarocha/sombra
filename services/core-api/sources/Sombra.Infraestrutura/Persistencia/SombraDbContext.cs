using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Autenticacao;
using Sombra.Infraestrutura.Mensageria;
using Sombra.Infraestrutura.Persistencia.Configuracoes;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>O banco do Core (PostgreSQL, ADR-003), que também é a unidade de trabalho dos casos de uso.</summary>
public sealed class SombraDbContext : DbContext, IUnitOfWork
{
    private readonly DespachanteDeFatos? _despachante;
    private readonly CifraDeCampos _cifra;

    /// <summary>Initializes a new instance of the <see cref="SombraDbContext"/> class, sem despachar fatos (migrações e testes).</summary>
    /// <param name="options">As opções, montadas por <see cref="Configurar"/>.</param>
    /// <param name="cifra">A cifra dos campos sensíveis (chaves do ambiente).</param>
    public SombraDbContext(DbContextOptions<SombraDbContext> options, CifraDeCampos cifra)
        : base(options)
        => _cifra = cifra ?? throw new ArgumentNullException(nameof(cifra));

    /// <summary>Initializes a new instance of the <see cref="SombraDbContext"/> class.</summary>
    /// <param name="options">As opções, montadas por <see cref="Configurar"/>.</param>
    /// <param name="cifra">A cifra dos campos sensíveis (chaves do ambiente).</param>
    /// <param name="despachante">Quem registra e publica os fatos depois de cada gravação.</param>
    public SombraDbContext(DbContextOptions<SombraDbContext> options, CifraDeCampos cifra, DespachanteDeFatos despachante)
        : this(options, cifra)
        => _despachante = despachante ?? throw new ArgumentNullException(nameof(despachante));

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

    /// <summary>Gets o outbox dos avisos.</summary>
    public DbSet<MensagemDeSaida> Outbox => Set<MensagemDeSaida>();

    /// <summary>Gets a trilha de segurança.</summary>
    public DbSet<EventoDeSeguranca> EventosDeSeguranca => Set<EventoDeSeguranca>();

    /// <summary>Gets as respostas guardadas por <c>Idempotency-Key</c>.</summary>
    public DbSet<RequisicaoIdempotente> RequisicoesIdempotentes => Set<RequisicaoIdempotente>();

    /// <summary>Monta as opções do contexto: Npgsql e nomes em snake_case (os mesmos no app e nas migrações).</summary>
    /// <param name="builder">O construtor de opções.</param>
    /// <param name="conexao">A string de conexão.</param>
    /// <returns>O mesmo construtor.</returns>
    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder builder, string conexao)
        => builder.UseNpgsql(conexao, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, ChaveDoModeloPorCifra>();

    /// <summary>Gets a impressão das chaves em uso, que separa o modelo do EF por par de chaves.</summary>
    internal string ImpressaoDaCifra => _cifra.Impressao;

    /// <summary>Cifra o que estiver em claro no banco (o seed e dados de antes da F2.3). Rode depois das migrações.</summary>
    /// <param name="cancellationToken">Cancela.</param>
    /// <returns>Quantas linhas foram cifradas.</returns>
    public Task<int> CifrarLegadoAsync(CancellationToken cancellationToken)
        => CifragemDoLegado.CifrarAsync((NpgsqlConnection)Database.GetDbConnection(), _cifra, cancellationToken);

    /// <inheritdoc/>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreencherIndicesCegos();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc/>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreencherIndicesCegos();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Grava tudo numa transação (agregados, leituras, check-ins e o outbox dos avisos) e só então despacha os fatos: log e
    /// tempo real. Os eventos de domínio são esquecidos depois de despachados.
    /// </summary>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    public async Task SalvarAsync(CancellationToken cancellationToken)
    {
        List<(Leitura Leitura, Guid DomicilioId)> leituras = [.. ChangeTracker.Entries<Leitura>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity, e.Property<Guid>(LeituraConfiguration.DomicilioId).CurrentValue))];
        List<CheckIn> checkIns = [.. ChangeTracker.Entries<CheckIn>().Where(e => e.State == EntityState.Added).Select(e => e.Entity)];
        List<AlertaDeCalor> alertas = [.. ChangeTracker.Entries<AlertaDeCalor>().Select(e => e.Entity).Where(a => a.EventosDeDominio.Count > 0)];

        await SaveChangesAsync(cancellationToken);

        FatosGravados fatos = new(leituras, checkIns, [.. alertas.Select(a => (a, (IReadOnlyList<EventoDeDominio>)[.. a.EventosDeDominio]))]);
        foreach (AlertaDeCalor alerta in alertas)
        {
            alerta.LimparEventos();
        }

        if (_despachante is not null)
        {
            await _despachante.DespacharAsync(fatos, cancellationToken);
        }
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SombraDbContext).Assembly);
        CamposSensiveis.Configurar(modelBuilder, _cifra);
    }

    // O índice cego acompanha o CPF em toda gravação: é ele que o índice único (casa, cpf_indice) compara.
    private void PreencherIndicesCegos()
    {
        foreach (EntityEntry<Morador> morador in ChangeTracker.Entries<Morador>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            morador.Property<string>(CamposSensiveis.CpfIndice).CurrentValue = _cifra.IndiceCego(morador.Entity.Cpf.Valor, CamposSensiveis.ColunaCpf);
        }
    }
}
