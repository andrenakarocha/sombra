using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Dominio.Tests.Alertas;

/// <summary>A máquina de estados dos alertas e o escalonamento em degraus (regras R4 a R8, D13).</summary>
[TestClass]
public sealed class AlertasTests
{
    private static readonly DateTimeOffset _abertura = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid _casa = Guid.CreateVersion7();

    /// <summary>Regra R6: o "estou bem" só é pedido a partir de cautela extrema, com prazo por nível.</summary>
    [TestMethod]
    public void AbreSoAPartirDeCautelaExtremaComPrazoPorNivel()
    {
        Assert.AreEqual("R6", Assert.ThrowsExactly<RegraVioladaException>(() => AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(30, 50, _abertura))).Regra);

        AlertaDeSentinela cautela = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(33, 45, _abertura));
        AlertaDeSentinela perigo = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));

        Assert.AreEqual(NivelDeRisco.CautelaExtrema, cautela.Nivel);
        Assert.AreEqual(_abertura.AddMinutes(30), cautela.Prazo);
        Assert.AreEqual(_abertura.AddMinutes(15), perigo.Prazo);
        Assert.AreEqual(EstadoDoAlerta.AguardandoCheckIn, perigo.Estado);
        Assert.IsInstanceOfType<AlertaAberto>(perigo.EventosDeDominio.Single());
    }

    /// <summary>Caminho A do roteiro: o "estou bem" no prazo encerra o alerta e mede o tempo até a resposta.</summary>
    [TestMethod]
    public void CaminhoAEstouBemEncerraOAlerta()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));
        alerta.ConfirmarEstouBem(_abertura.AddMinutes(6));

        Assert.AreEqual(EstadoDoAlerta.Resolvido, alerta.Estado);
        Assert.AreEqual(6.0, alerta.EventosDeDominio.OfType<AlertaEncerrado>().Single().MinutosAteResolver, 1e-9);
        Assert.AreEqual("R5", Assert.ThrowsExactly<RegraVioladaException>(() => alerta.ConfirmarEstouBem(_abertura.AddMinutes(7))).Regra);
    }

    /// <summary>Caminho B do roteiro: sem resposta em perigo, sobe lembrete leve, quem está perto e a rede toda.</summary>
    [TestMethod]
    public void CaminhoBSobeOsTresDegrausComVizinho()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));
        RedeDeCuidado rede = Rede();

        alerta.Escalonar(_abertura.AddMinutes(14), rede);
        Assert.AreEqual(DegrauDeEscalonamento.Sentinela, alerta.Degrau);

        alerta.Escalonar(_abertura.AddMinutes(15), rede);
        Assert.AreEqual(DegrauDeEscalonamento.LembreteLeve, alerta.Degrau);
        Assert.AreEqual(EstadoDoAlerta.Escalonado, alerta.Estado);
        Assert.AreEqual("Sua família foi avisada. Aperte o botão verde se você está bem.", alerta.MensagemAoMorador);

        alerta.Escalonar(_abertura.AddHours(2), rede);
        AlertaEscalonado[] degraus = [.. alerta.EventosDeDominio.OfType<AlertaEscalonado>()];
        CollectionAssert.AreEqual(
            new[] { DegrauDeEscalonamento.LembreteLeve, DegrauDeEscalonamento.QuemEstaPerto, DegrauDeEscalonamento.AlertaParaARede },
            degraus.Select(d => d.Degrau).ToArray());
        CollectionAssert.AreEqual(
            new[] { _abertura.AddMinutes(15), _abertura.AddMinutes(25), _abertura.AddMinutes(35) },
            degraus.Select(d => d.Momento).ToArray());
    }

    /// <summary>Sem vizinho na rede, o degrau 2 é pulado.</summary>
    [TestMethod]
    public void SemVizinhoPulaDoLembreteParaARede()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));
        RedeDeCuidado soAFilha = new();
        soAFilha.Definir([Fabricas.FilhaEVizinha()[0]]);

        alerta.Escalonar(_abertura.AddHours(1), soAFilha);

        CollectionAssert.AreEqual(
            new[] { DegrauDeEscalonamento.LembreteLeve, DegrauDeEscalonamento.AlertaParaARede },
            alerta.EventosDeDominio.OfType<AlertaEscalonado>().Select(d => d.Degrau).ToArray());
    }

    /// <summary>D13, pedido da filha: em cautela extrema, esquecer de apertar nunca passa do lembrete leve.</summary>
    [TestMethod]
    public void EmCautelaExtremaNuncaPassaDoLembreteLeve()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(33, 45, _abertura));
        alerta.Escalonar(_abertura.AddHours(6), Rede());

        Assert.AreEqual(DegrauDeEscalonamento.LembreteLeve, alerta.Degrau);
        Assert.AreEqual(1, alerta.EventosDeDominio.OfType<AlertaEscalonado>().Count());
    }

    /// <summary>"Vou ligar" para o escalonamento; "ela está bem" encerra.</summary>
    [TestMethod]
    public void CuidadorParaOuEncerraOEscalonamento()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));
        alerta.Escalonar(_abertura.AddMinutes(15), Rede());
        alerta.Reconhecer(AcaoDoCuidador.VouLigar, _abertura.AddMinutes(16));
        alerta.Escalonar(_abertura.AddHours(1), Rede());

        Assert.AreEqual(EstadoDoAlerta.Reconhecido, alerta.Estado);
        Assert.AreEqual(DegrauDeEscalonamento.LembreteLeve, alerta.Degrau);

        alerta.Reconhecer(AcaoDoCuidador.ElaEstaBem, _abertura.AddMinutes(20));
        Assert.AreEqual(EstadoDoAlerta.Resolvido, alerta.Estado);
        Assert.ThrowsExactly<RegraVioladaException>(() => alerta.Reconhecer(AcaoDoCuidador.EstouIndo, _abertura.AddMinutes(21)));
    }

    /// <summary>Regra R4: a leitura nova atualiza o alerta aberto; se o nível sobe, o prazo encurta e nunca aumenta.</summary>
    [TestMethod]
    public void LeituraNovaAtualizaOAlertaAberto()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(33, 45, _abertura));

        alerta.AtualizarComLeitura(Fabricas.Leitura(32, 40, _abertura.AddMinutes(5)));
        Assert.AreEqual(NivelDeRisco.CautelaExtrema, alerta.Nivel);

        alerta.AtualizarComLeitura(Fabricas.Leitura(36, 60, _abertura.AddMinutes(5)));
        Assert.AreEqual(NivelDeRisco.Perigo, alerta.Nivel);
        Assert.AreEqual(_abertura.AddMinutes(20), alerta.Prazo);
        Assert.IsInstanceOfType<AlertaAgravado>(alerta.EventosDeDominio[^1]);

        alerta.ConfirmarEstouBem(_abertura.AddMinutes(8));
        Assert.ThrowsExactly<RegraVioladaException>(() => alerta.AtualizarComLeitura(Fabricas.Leitura(36, 60, _abertura.AddMinutes(9))));
    }

    /// <summary>O preventivo só existe para previsão de perigo, não pede check-in e expira em 24 h.</summary>
    [TestMethod]
    public void PreventivoInformaEExpira()
    {
        Assert.ThrowsExactly<RegraVioladaException>(() => AlertaPreventivo.Abrir(_casa, NivelDeRisco.CautelaExtrema, _abertura));
        AlertaPreventivo alerta = AlertaPreventivo.Abrir(_casa, NivelDeRisco.Perigo, _abertura);

        Assert.AreEqual(EstadoDoAlerta.Informativo, alerta.Estado);
        Assert.IsNull(alerta.Prazo);
        Assert.ThrowsExactly<RegraVioladaException>(() => alerta.ConfirmarEstouBem(_abertura.AddHours(1)));
        alerta.Escalonar(_abertura.AddHours(23), Rede());
        Assert.AreEqual(DegrauDeEscalonamento.Sentinela, alerta.Degrau);

        Assert.IsFalse(alerta.ExpirarSeVencido(_abertura.AddHours(23)));
        Assert.IsTrue(alerta.ExpirarSeVencido(_abertura.AddHours(24)));
        Assert.AreEqual(EstadoDoAlerta.Expirado, alerta.Estado);
        Assert.IsFalse(alerta.ExpirarSeVencido(_abertura.AddHours(25)));
    }

    /// <summary>Regra R7: o pedido de ajuda avisa a rede toda na hora, em qualquer nível.</summary>
    [TestMethod]
    public void PedidoDeAjudaVaiDiretoAoDegrauTres()
    {
        AlertaDePedidoDeAjuda alerta = AlertaDePedidoDeAjuda.Abrir(_casa, NivelDeRisco.Normal, _abertura);

        Assert.AreEqual(DegrauDeEscalonamento.AlertaParaARede, alerta.Degrau);
        Assert.IsInstanceOfType<AlertaAberto>(alerta.EventosDeDominio[0]);
        Assert.AreEqual(DegrauDeEscalonamento.AlertaParaARede, ((AlertaEscalonado)alerta.EventosDeDominio[1]).Degrau);

        alerta.ConfirmarEstouBem(_abertura.AddMinutes(3));
        Assert.AreEqual(EstadoDoAlerta.Resolvido, alerta.Estado);
    }

    /// <summary>Regras R4 e R7: pedir ajuda num alerta de Sentinela leva o mesmo alerta ao degrau 3, uma vez só.</summary>
    [TestMethod]
    public void PedidoDeAjudaNoAlertaDeSentinelaVaiAoDegrauTres()
    {
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(33, 45, _abertura));

        alerta.PedirAjuda(_abertura.AddMinutes(2));
        alerta.PedirAjuda(_abertura.AddMinutes(3));
        alerta.Escalonar(_abertura.AddHours(1), Rede());

        Assert.AreEqual(DegrauDeEscalonamento.AlertaParaARede, alerta.Degrau);
        Assert.AreEqual(EstadoDoAlerta.Escalonado, alerta.Estado);
        Assert.AreEqual(1, alerta.EventosDeDominio.OfType<AlertaEscalonado>().Count());

        alerta.ConfirmarEstouBem(_abertura.AddMinutes(5));
        Assert.AreEqual("R5", Assert.ThrowsExactly<RegraVioladaException>(() => alerta.PedirAjuda(_abertura.AddMinutes(6))).Regra);
    }

    /// <summary>Regra R13: uma hora de carência depois do "estou bem", só para o mesmo nível ou abaixo.</summary>
    [TestMethod]
    public void CarenciaDepoisDoEstouBem()
    {
        AlertaDeSentinela resolvido = AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura));
        resolvido.ConfirmarEstouBem(_abertura.AddMinutes(5));
        AlertaDePedidoDeAjuda pedido = AlertaDePedidoDeAjuda.Abrir(_casa, NivelDeRisco.Perigo, _abertura);
        pedido.ConfirmarEstouBem(_abertura.AddMinutes(5));

        Assert.IsTrue(AlertaDeSentinela.EmCarencia(resolvido, Fabricas.Leitura(36, 60, _abertura.AddMinutes(10))));
        Assert.IsTrue(AlertaDeSentinela.EmCarencia(resolvido, Fabricas.Leitura(33, 45, _abertura.AddMinutes(10))));
        Assert.IsFalse(AlertaDeSentinela.EmCarencia(resolvido, Fabricas.Leitura(40, 80, _abertura.AddMinutes(10))));
        Assert.IsFalse(AlertaDeSentinela.EmCarencia(resolvido, Fabricas.Leitura(36, 60, _abertura.AddMinutes(5) + AlertaDeSentinela.Carencia)));
        Assert.IsFalse(AlertaDeSentinela.EmCarencia(pedido, Fabricas.Leitura(36, 60, _abertura.AddMinutes(10))));
        Assert.IsFalse(AlertaDeSentinela.EmCarencia(null, Fabricas.Leitura(36, 60, _abertura)));
    }

    /// <summary>As mensagens cabem no OLED (até 80 caracteres) e não têm dado pessoal.</summary>
    [TestMethod]
    public void MensagensCabemNoOled()
    {
        AlertaDeCalor[] alertas =
        [
            AlertaPreventivo.Abrir(_casa, NivelDeRisco.Perigo, _abertura),
            AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(33, 45, _abertura)),
            AlertaDeSentinela.Abrir(_casa, Fabricas.Leitura(36, 60, _abertura)),
            AlertaDePedidoDeAjuda.Abrir(_casa, NivelDeRisco.Perigo, _abertura),
        ];

        foreach (AlertaDeCalor alerta in alertas)
        {
            Assert.IsLessThanOrEqualTo(80, alerta.MensagemAoMorador.Length, alerta.Tipo);
            Assert.DoesNotContain("Lurdes", alerta.MensagemAoMorador, alerta.Tipo);
        }

        alertas[0].LimparEventos();
        Assert.IsEmpty(alertas[0].EventosDeDominio);
    }

    /// <summary>O check-in guarda o instante em UTC.</summary>
    [TestMethod]
    public void CheckInGuardaUtc()
    {
        CheckIn checkIn = CheckIn.Registrar(_casa, new DateTimeOffset(2026, 11, 18, 15, 0, 0, TimeSpan.FromHours(-3)), OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.EstouBem);

        Assert.AreEqual(TimeSpan.Zero, checkIn.Momento.Offset);
        Assert.AreEqual(18, checkIn.Momento.Hour);
    }

    private static RedeDeCuidado Rede()
    {
        RedeDeCuidado rede = new();
        rede.Definir(Fabricas.FilhaEVizinha());
        return rede;
    }
}
