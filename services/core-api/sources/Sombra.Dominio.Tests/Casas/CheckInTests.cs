using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Tests.Casas;

/// <summary>Regra R14: o check-in da fila offline vale no horário do toque, nunca no futuro e até 24 horas atrás.</summary>
[TestClass]
public sealed class CheckInTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid _casa = Guid.CreateVersion7();

    /// <summary>Sem horário, vale o agora; com horário, vale o do toque; um relógio pouco adiantado vira o agora.</summary>
    [TestMethod]
    public void ValeOHorarioDoToque()
    {
        Assert.AreEqual(_agora, Registrar(null).Momento);
        Assert.AreEqual(_agora.AddHours(-23), Registrar(_agora.AddHours(-23)).Momento);
        Assert.AreEqual(_agora, Registrar(_agora.AddMinutes(1)).Momento);
        Assert.AreEqual(_agora.AddHours(-3), Registrar(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.FromHours(-3))).Momento);
    }

    /// <summary>No futuro além da tolerância, ou de mais de 24 horas atrás, o check-in é recusado (R14).</summary>
    [TestMethod]
    public void RecusaForaDaJanela()
    {
        Assert.AreEqual("R14", Assert.ThrowsExactly<RegraVioladaException>(() => Registrar(_agora + CheckIn.ToleranciaDoRelogio + TimeSpan.FromSeconds(1))).Regra);
        Assert.AreEqual("R14", Assert.ThrowsExactly<RegraVioladaException>(() => Registrar(_agora - CheckIn.JanelaOffline - TimeSpan.FromSeconds(1))).Regra);
    }

    private static CheckIn Registrar(DateTimeOffset? tocadoEm)
        => CheckIn.RegistrarTocadoEm(_casa, tocadoEm, _agora, OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem);
}
