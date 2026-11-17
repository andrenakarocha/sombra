using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Sentinelas;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Dominio.Tests.Casas;

/// <summary>O domicílio é a raiz do agregado: regras R1, R3, R8, R9 e R10.</summary>
[TestClass]
public sealed class DomicilioTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Regra R8: sem morador ou sem contato, o monitoramento não liga.</summary>
    [TestMethod]
    public void MonitoramentoPedeMoradorEContato()
    {
        Domicilio casa = Fabricas.Casa();
        Assert.AreEqual("R8", Assert.ThrowsExactly<RegraVioladaException>(casa.AtivarMonitoramento).Regra);

        casa.AdicionarMorador(Fabricas.Morador(78));
        Assert.ThrowsExactly<RegraVioladaException>(casa.AtivarMonitoramento);

        casa.DefinirRedeDeCuidado(Fabricas.FilhaEVizinha());
        casa.AtivarMonitoramento();
        Assert.IsTrue(casa.Monitorado);
    }

    /// <summary>Regra R8: uma casa monitorada não fica sem contato nem sem morador.</summary>
    [TestMethod]
    public void CasaMonitoradaNaoPerdeOMinimo()
    {
        Domicilio casa = Fabricas.Casa();
        Morador lurdes = Fabricas.Morador(78);
        casa.AdicionarMorador(lurdes);
        casa.DefinirRedeDeCuidado(Fabricas.FilhaEVizinha());
        casa.AtivarMonitoramento();

        Assert.AreEqual("R8", Assert.ThrowsExactly<RegraVioladaException>(() => casa.DefinirRedeDeCuidado([])).Regra);
        Assert.AreEqual("R8", Assert.ThrowsExactly<RegraVioladaException>(() => casa.RemoverMorador(lurdes.Id)).Regra);

        casa.DesativarMonitoramento();
        Assert.IsTrue(casa.RemoverMorador(lurdes.Id));
    }

    /// <summary>Rede de cuidado: ordem preservada, filha no degrau 1, vizinha no degrau 2, sem telefone repetido.</summary>
    [TestMethod]
    public void RedeDeCuidadoGuardaOrdemEPapeis()
    {
        Domicilio casa = Fabricas.Casa();
        casa.DefinirRedeDeCuidado(Fabricas.FilhaEVizinha());

        Assert.AreEqual("Ana", casa.RedeDeCuidado.PrimeiroFamiliar?.Nome);
        Assert.AreEqual(PapelDoContato.Vizinho, casa.RedeDeCuidado.QuemEstaPerto?.Papel);

        ContatoDeCuidado repetido = new("Outra", Telefone.Criar("11987654321"), PapelDoContato.Familiar);
        Assert.ThrowsExactly<RegraVioladaException>(() => casa.DefinirRedeDeCuidado([.. Fabricas.FilhaEVizinha(), repetido]));
    }

    /// <summary>Regra R1: o mesmo CPF não entra duas vezes na casa.</summary>
    [TestMethod]
    public void NaoRepeteCpfNaCasa()
    {
        Domicilio casa = Fabricas.Casa();
        casa.AdicionarMorador(Fabricas.Morador(78, semente: 5));

        Assert.AreEqual("R1", Assert.ThrowsExactly<RegraVioladaException>(() => casa.AdicionarMorador(Fabricas.Morador(70, semente: 5))).Regra);
    }

    /// <summary>Regra R3: a casa usa os limites do morador mais vulnerável.</summary>
    [TestMethod]
    public void LeituraUsaOMoradorMaisVulneravel()
    {
        Domicilio casa = Fabricas.Casa();
        casa.VincularSentinela("snt-7f3a9c");
        Temperatura t = Temperatura.Criar(32.0);
        UmidadeRelativa ur = UmidadeRelativa.Criar(60.0);

        casa.AdicionarMorador(Fabricas.Morador(70, semente: 1));
        Assert.AreEqual(NivelDeRisco.CautelaExtrema, casa.RegistrarLeitura("snt-7f3a9c", _agora, t, ur).Nivel);

        casa.AdicionarMorador(Fabricas.Morador(85, 2, new UsoDeDiuretico()));
        Leitura leitura = casa.RegistrarLeitura("snt-7f3a9c", _agora.AddSeconds(10), t, ur);

        Assert.AreEqual(NivelDeRisco.Perigo, leitura.Nivel);
        Assert.AreEqual(37.07, leitura.IndiceDeCalor.Celsius, 0.05);
    }

    /// <summary>Regra R10: leitura de uma Sentinela de outra casa é recusada.</summary>
    [TestMethod]
    public void RecusaLeituraDeSentinelaDeOutraCasa()
    {
        Domicilio casa = Fabricas.Casa();
        casa.VincularSentinela("snt-7f3a9c");

        RegraVioladaException erro = Assert.ThrowsExactly<RegraVioladaException>(
            () => casa.RegistrarLeitura("snt-000001", _agora, Temperatura.Criar(30), UmidadeRelativa.Criar(50)));
        Assert.AreEqual("R10", erro.Regra);
        Assert.ThrowsExactly<RegraVioladaException>(() => casa.VincularSentinela("snt-111111"));
    }

    /// <summary>Regra R9: a Sentinela fica offline depois de três telemetrias perdidas.</summary>
    [TestMethod]
    public void SentinelaFicaOfflineDepoisDeTresTelemetriasPerdidas()
    {
        Domicilio casa = Fabricas.Casa();
        Sentinela sentinela = casa.VincularSentinela("SNT-7F3A9C");
        Assert.IsFalse(sentinela.EstaOnline(_agora));

        casa.RegistrarLeitura("snt-7f3a9c", _agora, Temperatura.Criar(30), UmidadeRelativa.Criar(50));

        Assert.IsTrue(sentinela.EstaOnline(_agora.AddSeconds(30)));
        Assert.IsFalse(sentinela.EstaOnline(_agora.AddSeconds(31)));
        Assert.AreEqual("R10", Assert.ThrowsExactly<RegraVioladaException>(() => Sentinela.Criar("sentinela-1")).Regra);
    }

    /// <summary>A casa nasce com a rotina padrão e guarda a rotina combinada com a pessoa (R11).</summary>
    [TestMethod]
    public void GuardaARotinaCombinada()
    {
        Domicilio casa = Fabricas.Casa();
        Assert.AreEqual(Rotina.Padrao, casa.Rotina);

        Rotina rotina = Rotina.Criar(new TimeOnly(9, 0), lembreteDeAguaAtivo: false);
        casa.DefinirRotina(rotina);

        Assert.AreEqual(rotina, casa.Rotina);
        Assert.ThrowsExactly<ArgumentNullException>(() => casa.DefinirRotina(null!));
    }

    /// <summary>Apelido e região climática são validados, e a casa nunca aparece pelo endereço.</summary>
    [TestMethod]
    public void ValidaCadastroDaCasa()
    {
        Endereco endereco = Endereco.Criar("Rua das Flores", "12", "Santana", "São Paulo", "SP", "02012000");

        Assert.ThrowsExactly<RegraVioladaException>(() => Domicilio.Cadastrar(" ", endereco, "SP-MIRANTE"));
        Assert.ThrowsExactly<RegraVioladaException>(() => Domicilio.Cadastrar("Casa", endereco, "mirante"));
        Assert.AreEqual("SP-MIRANTE", Domicilio.Cadastrar("Casa", endereco, " sp-mirante ").RegiaoClimatica);
        StringAssert.StartsWith(Fabricas.Casa().ToString(), "Domicilio ");
    }
}
