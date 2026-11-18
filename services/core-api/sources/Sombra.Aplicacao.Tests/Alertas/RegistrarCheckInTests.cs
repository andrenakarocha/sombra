using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Aplicacao.Tests.Alertas;

/// <summary>O "estou bem" e o "preciso de ajuda" (caminho A, R4, R7).</summary>
[TestClass]
public sealed class RegistrarCheckInTests
{
    /// <summary>Caminho A do roteiro: o botão verde encerra o alerta e guarda o check-in.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoAEstouBemEncerraOAlerta()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        await cenario.MedirAsync(36, 60);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(6));

        CheckInResposta resposta = await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.EstouBem);

        Assert.IsNotNull(resposta.Alerta);
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, resposta.Alerta.Estado);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(6), resposta.Alerta.EncerradoEm);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(6), resposta.Momento);
        Assert.HasCount(1, cenario.CheckIns.Itens);
    }

    /// <summary>O "estou bem" sem alerta aberto (o check-in diário, R11) só fica registrado.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task EstouBemSemAlertaSoRegistra()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();

        CheckInResposta resposta = await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem);

        Assert.IsNull(resposta.Alerta);
        Assert.HasCount(1, cenario.CheckIns.Itens);
        Assert.IsEmpty(cenario.Notificador.Avisos);
    }

    /// <summary>Regra R7: o pedido de ajuda sem alerta aberto abre um, já no degrau 3, e avisa a rede toda na hora.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PedidoDeAjudaSemAlertaAvisaARedeToda()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        await cenario.MedirAsync(30, 50);

        CheckInResposta resposta = await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.App, SituacaoDoCheckIn.PrecisoDeAjuda);

        Assert.IsNotNull(resposta.Alerta);
        Assert.AreEqual(TipoDoAlertaNoApp.DeSentinela, resposta.Alerta.Tipo);
        Assert.AreEqual("Pedido de ajuda pela Sentinela ou pelo app.", resposta.Alerta.LinhaDoTempo[0].Descricao);
        Assert.AreEqual(NivelDeRisco.Atencao, resposta.Alerta.Nivel);
        Assert.AreEqual(3, resposta.Alerta.Degrau);
        Notificacao rede = cenario.Notificador.Avisos.Single(a => a.Destinatario == Destinatario.Rede);
        Assert.HasCount(2, rede.Contatos);
        Assert.Contains("pedido de ajuda", rede.Mensagem);
        Assert.AreEqual(Destinatario.Morador, cenario.Notificador.Avisos[^1].Destinatario);
    }

    /// <summary>Regras R4 e R7: pedir ajuda durante um alerta de Sentinela leva o mesmo alerta ao degrau 3, uma vez só.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PedidoDeAjudaNoAlertaAbertoNaoAbreOutro()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        await cenario.MedirAsync(33, 45);

        CheckInResposta primeiro = await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.PrecisoDeAjuda);
        CheckInResposta segundo = await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.App, SituacaoDoCheckIn.PrecisoDeAjuda);

        Assert.IsNotNull(primeiro.Alerta);
        Assert.AreEqual(TipoDoAlertaNoApp.DeSentinela, primeiro.Alerta.Tipo);
        Assert.AreEqual(3, primeiro.Alerta.Degrau);
        Assert.AreEqual(primeiro.Alerta.Id, segundo.Alerta?.Id);
        Assert.HasCount(1, cenario.Alertas.Alertas);
        Assert.AreEqual(1, cenario.Notificador.Avisos.Count(a => a.Destinatario == Destinatario.Rede));
    }

    /// <summary>Casa inexistente dá 404; check-in sem origem e situação dá 400.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RecusaCasaInexistenteECampoFaltando()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();

        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => cenario.CheckInAsync(Guid.CreateVersion7(), OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem));
        ValidacaoException vazio = await Assert.ThrowsExactlyAsync<ValidacaoException>(
            () => cenario.RegistrarCheckIn().ExecutarAsync(casa.Id, new NovoCheckInDto(), CancellationToken.None));

        Assert.HasCount(2, vazio.Erros);
        Assert.IsEmpty(cenario.CheckIns.Itens);
    }

    /// <summary>
    /// F6.23 e R14: o "estou bem" que esperou a rede vale no horário do toque. O tempo até o check-in sai certo, e um toque
    /// de antes de o alerta abrir não responde a ele.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CheckInDaFilaOfflineValeNoHorarioDoToque()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        await cenario.MedirAsync(36, 60);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(40));
        RegistrarCheckIn checkIn = cenario.RegistrarCheckIn();

        CheckInResposta antesDoAlerta = await checkIn.ExecutarAsync(
            casa.Id, new NovoCheckInDto { Origem = OrigemDoCheckIn.App, Situacao = SituacaoDoCheckIn.EstouBem, Momento = Cenario.Inicio.AddMinutes(-5) }, CancellationToken.None);
        CheckInResposta doToque = await checkIn.ExecutarAsync(
            casa.Id, new NovoCheckInDto { Origem = OrigemDoCheckIn.App, Situacao = SituacaoDoCheckIn.EstouBem, Momento = Cenario.Inicio.AddMinutes(7) }, CancellationToken.None);
        RegraVioladaException futuro = await Assert.ThrowsExactlyAsync<RegraVioladaException>(() => checkIn.ExecutarAsync(
            casa.Id, new NovoCheckInDto { Origem = OrigemDoCheckIn.App, Situacao = SituacaoDoCheckIn.EstouBem, Momento = cenario.Relogio.Agora.AddMinutes(5) }, CancellationToken.None));

        Assert.IsNull(antesDoAlerta.Alerta);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(-5), antesDoAlerta.Momento);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(7), doToque.Momento);
        Assert.AreEqual(EstadoDoAlertaNoApp.Resolvido, doToque.Alerta?.Estado);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(7), doToque.Alerta?.EncerradoEm);
        Assert.AreEqual("Apertou \"estou bem\" no celular.", doToque.Alerta?.LinhaDoTempo[^1].Descricao);
        Assert.AreEqual("R14", futuro.Regra);
    }
}
