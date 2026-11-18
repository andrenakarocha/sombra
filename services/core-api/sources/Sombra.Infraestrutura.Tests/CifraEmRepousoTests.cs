using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Tests.Apoio;
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Tests;

/// <summary>F2.3: o que o banco guarda de PII está cifrado, o índice cego faz a unicidade e texto em claro é erro.</summary>
[TestClass]
public sealed class CifraEmRepousoTests
{
    /// <summary>O CPF da Dona Lurdes no seed (sintético).</summary>
    private const string CpfDaLurdes = "34875763689";

    /// <summary>
    /// Depois das migrações, o seed que nasceu em claro está todo cifrado (CPF, Cartão SUS, perfil e rede), com o índice
    /// cego do CPF; cifrar o legado de novo não acha nada.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task SeedFicaCifradoEOLegadoNaoVoltaASerCifrado()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using SombraDbContext db = criar();

        List<string?[]> moradores = await LinhasAsync(db, moradores: true);
        List<string?[]> redes = await LinhasAsync(db, moradores: false);

        Assert.HasCount(3, moradores);
        Assert.IsTrue(moradores.All(m => CifraDeCampos.EstaCifrado(m[0]) && CifraDeCampos.EstaCifrado(m[2]) && (m[1] is null || CifraDeCampos.EstaCifrado(m[1]))));
        Assert.IsTrue(redes.All(r => CifraDeCampos.EstaCifrado(r[0])));
        Assert.IsTrue(moradores.Any(m => m[3] == BancoDeTeste.Cifra.IndiceCego(CpfDaLurdes, "moradores.cpf")));
        Assert.IsFalse(moradores.Concat(redes).SelectMany(l => l).Any(v => v?.Contains(CpfDaLurdes, StringComparison.Ordinal) == true || v?.Contains("Ana (filha)", StringComparison.Ordinal) == true));
        Assert.AreEqual(0, await db.CifrarLegadoAsync(CancellationToken.None));
    }

    /// <summary>O índice único (casa, índice cego) recusa o mesmo CPF duas vezes na casa, sem o banco ver o CPF.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task IndiceCegoBarraCpfRepetidoNaCasa()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using SombraDbContext db = criar();
        string indice = BancoDeTeste.Cifra.IndiceCego(CpfDaLurdes, "moradores.cpf");
        string cpf = BancoDeTeste.Cifra.Cifrar(CpfDaLurdes, "moradores.cpf");
        string perfil = BancoDeTeste.Cifra.Cifrar(string.Empty, "moradores.perfil_de_saude");

        PostgresException erro = await Assert.ThrowsExactlyAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO moradores (id, domicilio_id, nome, data_de_nascimento, cpf, cpf_indice, perfil_de_saude) VALUES ({Guid.NewGuid()}, {Casas.Lurdes}, 'Cópia', '1950-01-01', {cpf}, {indice}, {perfil})"));

        Assert.AreEqual(PostgresErrorCodes.UniqueViolation, erro.SqlState);
    }

    /// <summary>Um CPF gravado em claro por fora do Core não é lido como se nada fosse: a leitura falha alto.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task TextoEmClaroNoBancoFalhaAoLer()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using (SombraDbContext db = criar())
        {
            await db.Database.ExecuteSqlAsync($"UPDATE moradores SET cpf = {CpfDaLurdes} WHERE domicilio_id = {Casas.Lurdes}");
        }

        await using SombraDbContext outro = criar();
        await Assert.ThrowsExactlyAsync<CryptographicException>(() => new DomicilioRepository(outro).ObterAsync(Casas.Lurdes, CancellationToken.None));
    }

    private static async Task<List<string?[]>> LinhasAsync(SombraDbContext db, bool moradores)
    {
        DbConnection conexao = db.Database.GetDbConnection();
        await conexao.OpenAsync();
        try
        {
            await using DbCommand comando = conexao.CreateCommand();
            if (moradores)
            {
                comando.CommandText = "SELECT cpf, cartao_sus, perfil_de_saude, cpf_indice FROM moradores";
            }
            else
            {
                comando.CommandText = "SELECT rede_de_cuidado FROM domicilios";
            }

            await using DbDataReader leitor = await comando.ExecuteReaderAsync();
            List<string?[]> linhas = [];
            while (await leitor.ReadAsync())
            {
                linhas.Add([.. Enumerable.Range(0, leitor.FieldCount).Select(i => leitor.IsDBNull(i) ? null : leitor.GetString(i))]);
            }

            return linhas;
        }
        finally
        {
            await conexao.CloseAsync();
        }
    }
}
