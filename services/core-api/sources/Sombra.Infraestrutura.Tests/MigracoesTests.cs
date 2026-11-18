using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Saude;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Tests.Apoio;

namespace Sombra.Infraestrutura.Tests;

/// <summary>O banco nasce do zero pelas migrações, com o seed da demonstração.</summary>
[TestClass]
public sealed class MigracoesTests
{
    /// <summary>As migrações, em ordem, e a casa da Dona Lurdes inteira no seed.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task BancoNasceDoZeroComAsMigracoesEOSeed()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using SombraDbContext db = criar();
        DomicilioRepository casas = new(db);
        string[] esperadas = ["Inicial", "AlertasECheckIns", "RedeDeCuidadoEIndices", "SeedDemo", "Usuarios", "MqttOutboxESeguranca", "LinhaDoTempoETelefoneDoMorador", "EscritasDoApp", "Idempotencia", "CamposSensiveisCifrados", "EventosDePrivacidade"];

        string[] aplicadas = [.. (await db.Database.GetAppliedMigrationsAsync()).Select(m => m[(m.IndexOf('_', StringComparison.Ordinal) + 1)..])];
        Domicilio? lurdes = await casas.ObterAsync(Casas.Lurdes, CancellationToken.None);

        CollectionAssert.AreEqual(esperadas, aplicadas);
        Assert.IsNotNull(lurdes);
        Assert.AreEqual("Casa da Dona Lurdes", lurdes.Apelido);
        Assert.AreEqual("Santana", lurdes.Endereco.Bairro);
        Assert.IsTrue(lurdes.Monitorado);
        Assert.AreEqual(new TimeOnly(9, 0), lurdes.Rotina.HorarioDoCheckInDiario);
        Morador moradora = lurdes.Moradores.Single();
        Assert.AreEqual(78, moradora.Idade(DateOnly.FromDateTime(Casas.Agora.UtcDateTime)));
        Assert.AreEqual(PerfilDeSaude.Com([new UsoDeDiuretico()]), moradora.PerfilDeSaude);
        Assert.IsNotNull(moradora.CartaoSus);
        Assert.AreEqual("Ana (filha)", lurdes.RedeDeCuidado.PrimeiroFamiliar?.Nome);
        Assert.AreEqual("Rosa (vizinha)", lurdes.RedeDeCuidado.QuemEstaPerto?.Nome);
        Assert.AreEqual("snt-7f3a9c", lurdes.Sentinelas.Single().DeviceId);
        Assert.HasCount(2, await casas.ListarMonitoradosAsync(CancellationToken.None));
    }
}
