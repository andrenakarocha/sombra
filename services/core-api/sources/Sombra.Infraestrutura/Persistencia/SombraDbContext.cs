using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>O banco do Core (PostgreSQL, ADR-003), que também é a unidade de trabalho dos casos de uso.</summary>
public sealed class SombraDbContext : DbContext, IUnitOfWork
{
    /// <summary>Initializes a new instance of the <see cref="SombraDbContext"/> class.</summary>
    /// <param name="options">As opções, montadas por <see cref="Configurar"/>.</param>
    public SombraDbContext(DbContextOptions<SombraDbContext> options)
        : base(options)
    {
    }

    /// <summary>Gets as casas, com moradores e Sentinelas.</summary>
    public DbSet<Domicilio> Domicilios => Set<Domicilio>();

    /// <summary>Gets as leituras das Sentinelas.</summary>
    public DbSet<Leitura> Leituras => Set<Leitura>();

    /// <summary>Gets os alertas, de todos os tipos.</summary>
    public DbSet<AlertaDeCalor> Alertas => Set<AlertaDeCalor>();

    /// <summary>Gets os check-ins.</summary>
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    /// <summary>Monta as opções do contexto: Npgsql e nomes em snake_case (os mesmos no app e nas migrações).</summary>
    /// <param name="builder">O construtor de opções.</param>
    /// <param name="conexao">A string de conexão.</param>
    /// <returns>O mesmo construtor.</returns>
    public static DbContextOptionsBuilder Configurar(DbContextOptionsBuilder builder, string conexao)
        => builder.UseNpgsql(conexao, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)).UseSnakeCaseNamingConvention();

    /// <inheritdoc/>
    public Task SalvarAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SombraDbContext).Assembly);
}
