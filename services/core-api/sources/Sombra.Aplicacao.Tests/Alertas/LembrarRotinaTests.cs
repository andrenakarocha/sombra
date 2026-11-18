using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Tests.Alertas;

/// <summary>A rotina combinada: lembrete de água no pico da tarde e check-in diário esquecido (R11, R12).</summary>
[TestClass]
public sealed class LembrarRotinaTests
{
    /// <summary>Num dia quente, às 15h e às 15h30: dois lembretes de água, nunca seguidos e nunca depois das 16h.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AguaNoPicoDaTardeDeUmDiaQuente()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        await cenario.MedirAsync(30, 50);
        LembrarRotina rotina = cenario.LembrarRotina();

        int[] voltas = new int[4];
        voltas[0] = await rotina.ExecutarAsync(CancellationToken.None);
        voltas[1] = await rotina.ExecutarAsync(CancellationToken.None);
        cenario.Relogio.Avancar(LembrarRotina.IntervaloEntreLembretesDeAgua);
        voltas[2] = await rotina.ExecutarAsync(CancellationToken.None);
        cenario.Relogio.Avancar(LembrarRotina.IntervaloEntreLembretesDeAgua);
        voltas[3] = await rotina.ExecutarAsync(CancellationToken.None);

        int[] esperado = [1, 0, 1, 0];
        CollectionAssert.AreEqual(esperado, voltas);
        Notificacao[] agua = [.. cenario.Notificador.Avisos.Where(a => a.Motivo == MotivoDoAviso.LembreteDeAgua)];
        Assert.HasCount(2, agua);
        Assert.IsTrue(agua.All(a => a.Destinatario == Destinatario.Morador && a.AlertaId is null));
    }

    /// <summary>Sem calor não há lembrete de água; à noite não há lembrete nenhum (R12).</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task SemCalorENemDeNoite()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        casa.DefinirRotina(Rotina.Criar(new TimeOnly(9, 0), lembreteDeAguaAtivo: true));
        await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem);
        await cenario.MedirAsync(24, 50);

        int diaAmeno = await cenario.LembrarRotina().ExecutarAsync(CancellationToken.None);
        cenario.Relogio.Avancar(TimeSpan.FromHours(8));
        await cenario.MedirAsync(33, 45);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(1));
        int noite = await cenario.LembrarRotina().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(0, diaAmeno);
        Assert.AreEqual(0, noite);
        Assert.IsFalse(cenario.Notificador.Avisos.Any(a => a.Motivo is MotivoDoAviso.LembreteDeAgua or MotivoDoAviso.CheckInDiario));
    }

    /// <summary>R11: o check-in diário esquecido gera um lembrete na tela e um lembrete leve ao familiar, uma vez por dia.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CheckInDiarioEsquecidoViraSoLembreteLeve()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        casa.DefinirRotina(Rotina.Criar(new TimeOnly(9, 0), lembreteDeAguaAtivo: false));
        LembrarRotina rotina = cenario.LembrarRotina();

        int primeira = await rotina.ExecutarAsync(CancellationToken.None);
        int segunda = await rotina.ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(1, primeira);
        Assert.AreEqual(0, segunda);
        Notificacao[] lembretes = [.. cenario.Notificador.Avisos.Where(a => a.Motivo == MotivoDoAviso.CheckInDiario)];
        Destinatario[] destinatarios = [Destinatario.Morador, Destinatario.Familiar];
        CollectionAssert.AreEqual(destinatarios, lembretes.Select(a => a.Destinatario).ToArray());
        Assert.AreEqual("Ana", lembretes[1].Contatos.Single().Nome);
        Assert.IsEmpty(cenario.Alertas.Alertas);
    }
}
