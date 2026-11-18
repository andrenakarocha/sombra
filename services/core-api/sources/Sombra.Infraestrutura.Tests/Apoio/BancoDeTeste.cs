using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Sombra.Infraestrutura.Persistencia;
using Testcontainers.PostgreSql;

namespace Sombra.Infraestrutura.Tests.Apoio;

/// <summary>
/// Um PostgreSQL 16 de verdade (a mesma imagem do compose) para todos os testes, com um banco novo por teste: o
/// <c>MigrateAsync</c> cria o banco do zero e aplica as quatro migrações, seed incluído.
/// </summary>
[TestClass]
public sealed class BancoDeTeste
{
    private static PostgreSqlContainer? _postgres;

    /// <summary>Sobe o contêiner uma vez para a montagem inteira.</summary>
    /// <param name="contexto">O contexto do MSTest.</param>
    /// <returns>A subida.</returns>
    [AssemblyInitialize]
    public static async Task IniciarAsync(TestContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await _postgres.StartAsync(contexto.CancellationToken);
    }

    /// <summary>Derruba o contêiner.</summary>
    /// <returns>A descida.</returns>
    [AssemblyCleanup]
    public static async Task EncerrarAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>Cria um banco só para o teste, com as migrações aplicadas.</summary>
    /// <returns>Uma fábrica de contextos, um por "requisição".</returns>
    internal static async Task<Func<SombraDbContext>> NovoBancoAsync()
    {
        string conexao = new NpgsqlConnectionStringBuilder(_postgres!.GetConnectionString()) { Database = $"sombra_{Guid.NewGuid():N}" }.ConnectionString;

        SombraDbContext Criar()
        {
            DbContextOptionsBuilder<SombraDbContext> opcoes = new();
            SombraDbContext.Configurar(opcoes, conexao);
            return new SombraDbContext(opcoes.Options);
        }

        await using SombraDbContext db = Criar();
        await db.Database.MigrateAsync();
        return Criar;
    }
}
