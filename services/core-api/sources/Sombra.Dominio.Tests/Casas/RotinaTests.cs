using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Tests.Casas;

/// <summary>A rotina da D13: lembrete de água no pico da tarde e check-in diário opcional, nunca à noite (R11, R12).</summary>
[TestClass]
public sealed class RotinaTests
{
    /// <summary>O lembrete de água sai só no pico, em dia quente e até duas vezes.</summary>
    /// <param name="hora">A hora local.</param>
    /// <param name="nivel">O nível do dia.</param>
    /// <param name="jaEnviados">Lembretes já enviados hoje.</param>
    /// <param name="esperado">Se o lembrete sai.</param>
    [TestMethod]
    [DataRow("15:00", NivelDeRisco.Atencao, 0, true)]
    [DataRow("15:59", NivelDeRisco.Perigo, 1, true)]
    [DataRow("16:00", NivelDeRisco.Perigo, 0, false)]
    [DataRow("14:59", NivelDeRisco.Perigo, 0, false)]
    [DataRow("15:30", NivelDeRisco.Normal, 0, false)]
    [DataRow("15:30", NivelDeRisco.Perigo, 2, false)]
    public void LembreteDeAguaNoPicoDaTarde(string hora, NivelDeRisco nivel, int jaEnviados, bool esperado)
        => Assert.AreEqual(esperado, Rotina.Padrao.DeveLembrarAgua(TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture), nivel, jaEnviados));

    /// <summary>Regra R12: a noite vai das 22h às 7h, e o check-in diário não pode cair nela.</summary>
    [TestMethod]
    public void NoiteNaoTemLembrete()
    {
        Assert.IsTrue(Rotina.EhNoite(new TimeOnly(22, 0)));
        Assert.IsTrue(Rotina.EhNoite(new TimeOnly(6, 59)));
        Assert.IsFalse(Rotina.EhNoite(new TimeOnly(7, 0)));
        Assert.AreEqual("R12", Assert.ThrowsExactly<RegraVioladaException>(() => Rotina.Criar(new TimeOnly(23, 0), true)).Regra);
        Assert.IsFalse(Rotina.Criar(null, false).DeveLembrarAgua(new TimeOnly(15, 30), NivelDeRisco.Perigo, 0));
    }

    /// <summary>Regra R11: o check-in diário esquecido só conta depois da tolerância, e só se ainda não houve check-in.</summary>
    [TestMethod]
    public void CheckInDiarioEsquecido()
    {
        Rotina rotina = Rotina.Criar(new TimeOnly(9, 0), true);

        Assert.IsFalse(rotina.CheckInDiarioEsquecido(new TimeOnly(9, 29), jaFezCheckInHoje: false));
        Assert.IsTrue(rotina.CheckInDiarioEsquecido(new TimeOnly(9, 30), jaFezCheckInHoje: false));
        Assert.IsFalse(rotina.CheckInDiarioEsquecido(new TimeOnly(12, 0), jaFezCheckInHoje: true));
        Assert.IsFalse(Rotina.Padrao.CheckInDiarioEsquecido(new TimeOnly(12, 0), jaFezCheckInHoje: false));
    }

    /// <summary>
    /// D17: como no app, o horário fica guardado com o check-in diário desligado, e a R12 só vale com ele ligado; o padrão
    /// é desligado, às 9h.
    /// </summary>
    [TestMethod]
    public void CheckInDesligadoGuardaOHorario()
    {
        Rotina desligado = Rotina.Criar(checkInDiarioAtivo: false, new TimeOnly(23, 30), lembreteDeAguaAtivo: false);
        Rotina ligado = Rotina.Criar(checkInDiarioAtivo: true, new TimeOnly(10, 15), lembreteDeAguaAtivo: true);

        Assert.AreEqual(new TimeOnly(23, 30), desligado.HorarioEscolhido);
        Assert.IsNull(desligado.HorarioDoCheckInDiario);
        Assert.IsFalse(desligado.CheckInDiarioEsquecido(new TimeOnly(12, 0), jaFezCheckInHoje: false));
        Assert.AreEqual(new TimeOnly(10, 15), ligado.HorarioDoCheckInDiario);
        Assert.AreEqual("R12", Assert.ThrowsExactly<RegraVioladaException>(() => Rotina.Criar(checkInDiarioAtivo: true, new TimeOnly(6, 59), lembreteDeAguaAtivo: true)).Regra);
        Assert.IsFalse(Rotina.Padrao.CheckInDiarioAtivo);
        Assert.AreEqual(new TimeOnly(9, 0), Rotina.Padrao.HorarioEscolhido);
    }
}
