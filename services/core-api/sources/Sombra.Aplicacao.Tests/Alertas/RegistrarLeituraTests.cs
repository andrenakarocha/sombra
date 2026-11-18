using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Aplicacao.Tests.Alertas;

/// <summary>A telemetria da Sentinela abre e atualiza alertas (R4, R6, R9).</summary>
[TestClass]
public sealed class RegistrarLeituraTests
{
    /// <summary>Roteiro, passo 3: cautela extrema numa casa monitorada abre o alerta e muda a tela da Sentinela.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CautelaExtremaAbreOAlertaEAvisaASentinela()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();

        LeituraRegistradaResposta resposta = await cenario.MedirAsync(33, 45);

        Assert.AreEqual(NivelDeRisco.CautelaExtrema, resposta.Leitura.Nivel);
        Assert.IsNotNull(resposta.Alerta);
        Assert.AreEqual("DE_SENTINELA", resposta.Alerta.Tipo);
        Assert.AreEqual(EstadoDoAlerta.AguardandoCheckIn, resposta.Alerta.Estado);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(30), resposta.Alerta.Prazo);
        Notificacao aviso = cenario.Notificador.Avisos.Single();
        Assert.AreEqual(Destinatario.Morador, aviso.Destinatario);
        Assert.AreEqual(casa.Id, aviso.DomicilioId);
        Assert.AreEqual(resposta.Alerta.MensagemAoMorador, aviso.Mensagem);
        Assert.HasCount(1, cenario.Leituras.Itens);
        Assert.AreEqual(1, cenario.UnidadeDeTrabalho.Gravacoes);
    }

    /// <summary>Regra R4: a leitura seguinte atualiza o mesmo alerta; quando o nível sobe, o prazo encurta e a tela muda.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LeituraMaisQuenteAtualizaOMesmoAlerta()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();

        LeituraRegistradaResposta primeira = await cenario.MedirAsync(33, 45);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(5));
        LeituraRegistradaResposta segunda = await cenario.MedirAsync(36, 60);
        await cenario.MedirAsync(36, 60);

        Assert.IsNotNull(segunda.Alerta);
        Assert.AreEqual(primeira.Alerta?.Id, segunda.Alerta.Id);
        Assert.HasCount(1, cenario.Alertas.Alertas);
        Assert.AreEqual(NivelDeRisco.Perigo, segunda.Alerta.Nivel);
        Assert.AreEqual(Cenario.Inicio.AddMinutes(20), segunda.Alerta.Prazo);
        Assert.HasCount(2, cenario.Notificador.Avisos);
    }

    /// <summary>Abaixo de cautela extrema (R6) ou sem monitoramento, a leitura fica guardada e nenhum alerta abre.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task SemRiscoOuSemMonitoramentoSoGuardaALeitura()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();

        LeituraRegistradaResposta atencao = await cenario.MedirAsync(30, 50);
        casa.DesativarMonitoramento();
        LeituraRegistradaResposta semMonitorar = await cenario.MedirAsync(36, 60);

        Assert.AreEqual(NivelDeRisco.Atencao, atencao.Leitura.Nivel);
        Assert.IsNull(atencao.Alerta);
        Assert.IsNull(semMonitorar.Alerta);
        Assert.IsEmpty(cenario.Alertas.Alertas);
        Assert.IsEmpty(cenario.Notificador.Avisos);
        Assert.HasCount(2, cenario.Leituras.Itens);
    }

    /// <summary>
    /// Regra R13: logo depois do "estou bem", a casa ainda quente não pede o botão de novo; um nível mais alto reabre, e
    /// passada uma hora também.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CarenciaDepoisDoEstouBem()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        await cenario.MedirAsync(36, 60);
        await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.EstouBem);

        cenario.Relogio.Avancar(TimeSpan.FromMinutes(1));
        LeituraRegistradaResposta aindaQuente = await cenario.MedirAsync(36, 60);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(1));
        LeituraRegistradaResposta piorou = await cenario.MedirAsync(40, 80);
        await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.EstouBem);
        cenario.Relogio.Avancar(AlertaDeSentinela.Carencia);
        LeituraRegistradaResposta passouAHora = await cenario.MedirAsync(36, 60);

        Assert.IsNull(aindaQuente.Alerta);
        Assert.AreEqual(NivelDeRisco.PerigoExtremo, piorou.Alerta?.Nivel);
        Assert.IsNotNull(passouAHora.Alerta);
        Assert.HasCount(3, cenario.Alertas.Alertas);
    }

    /// <summary>Regra R4 como o Sombra a aplica: o preventivo da véspera não impede o alerta de Sentinela.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PreventivoConviveComOAlertaDeSentinela()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        cenario.Alertas.Adicionar(AlertaPreventivo.Abrir(casa.Id, NivelDeRisco.Perigo, Cenario.Inicio.AddHours(-12)));

        LeituraRegistradaResposta resposta = await cenario.MedirAsync(36, 60);

        Assert.AreEqual("DE_SENTINELA", resposta.Alerta?.Tipo);
        Assert.AreEqual(2, cenario.Alertas.Alertas.Count(a => a.EstaAberto));
    }

    /// <summary>Entradas ruins: formato dá 400, Sentinela sem casa dá 404 e faixa física (R9) dá 422, sem gravar nada.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task RecusaEntradasRuinsSemGravar()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        string[] camposEsperados = ["deviceId", "momento", "temperaturaC", "umidadePct"];

        ValidacaoException formato = await Assert.ThrowsExactlyAsync<ValidacaoException>(
            () => cenario.RegistrarLeitura().ExecutarAsync(new NovaLeituraDto { DeviceId = "SNT-1" }, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => cenario.MedirAsync(30, 50, "snt-000000"));
        RegraVioladaException faixa = await Assert.ThrowsExactlyAsync<RegraVioladaException>(() => cenario.MedirAsync(75, 50));

        CollectionAssert.AreEquivalent(camposEsperados, formato.Erros.Keys.ToArray());
        Assert.AreEqual("R9", faixa.Regra);
        Assert.AreEqual(0, cenario.UnidadeDeTrabalho.Gravacoes);
        Assert.IsEmpty(cenario.Leituras.Itens);
    }
}
