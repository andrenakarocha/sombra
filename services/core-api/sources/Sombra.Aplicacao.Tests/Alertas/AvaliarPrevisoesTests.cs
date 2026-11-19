using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Saude;

namespace Sombra.Aplicacao.Tests.Alertas;

/// <summary>O monitor de previsão abre o alerta preventivo (roteiro, passo 2).</summary>
[TestClass]
public sealed class AvaliarPrevisoesTests
{
    /// <summary>Previsão de perigo abre um preventivo por casa, muda a tela e avisa a filha; o Clima é consultado uma vez por região.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PrevisaoDePerigoAbreUmPreventivoPorCasa()
    {
        Cenario cenario = new();
        Domicilio lurdes = cenario.CasaMonitorada();
        cenario.CasaMonitorada(deviceId: "snt-00000a");
        cenario.CasaMonitorada("SP-SE", "snt-00000b");
        cenario.Clima.Riscos["SP-MIRANTE"] = new RiscoDaRegiao(NivelDeRisco.Atencao, NivelDeRisco.Perigo, 0.83, "onnx");
        cenario.Clima.Riscos["SP-SE"] = new RiscoDaRegiao(NivelDeRisco.Normal, NivelDeRisco.Atencao, 0.10, "onnx");

        int abertos = await cenario.AvaliarPrevisoes().ExecutarAsync(CancellationToken.None);
        int deNovo = await cenario.AvaliarPrevisoes().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(2, abertos);
        Assert.AreEqual(0, deNovo);
        Assert.AreEqual(4, cenario.Clima.Consultas);
        AlertaDeCalor preventivo = cenario.Alertas.Alertas.Single(a => a.DomicilioId == lurdes.Id);
        Assert.AreEqual("PREVENTIVO", preventivo.Tipo);
        Notificacao[] avisos = [.. cenario.Notificador.Avisos.Where(a => a.DomicilioId == lurdes.Id)];
        Assert.AreEqual("Amanhã: calor perigoso. Encha sua garrafa de água.", avisos[0].Mensagem);
        Assert.AreEqual(Destinatario.Familiar, avisos[1].Destinatario);
        Assert.AreEqual("Ana", avisos[1].Contatos.Single().Nome);
    }

    /// <summary>
    /// DA6: com o pico real de nov/2023 (a região em CAUTELA_EXTREMA, pico de 35,47 °C), o preventivo abre só na casa de
    /// quem tem 85 anos e cardiopatia, com o nível pessoal de perigo.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PicoRealAbrePreventivoSoNaCasaVulneravel()
    {
        Cenario cenario = new();
        Domicilio comum = cenario.CasaMonitorada();
        Domicilio vulneravel = cenario.CasaMonitorada(deviceId: "snt-00000c", idade: 85, fatores: new Cardiopatia());
        cenario.Clima.Riscos["SP-MIRANTE"] = new RiscoDaRegiao(NivelDeRisco.CautelaExtrema, NivelDeRisco.CautelaExtrema, 0.07, "regra", 35.47);

        int abertos = await cenario.AvaliarPrevisoes().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(1, abertos);
        AlertaDeCalor preventivo = cenario.Alertas.Alertas.Single();
        Assert.AreEqual(vulneravel.Id, preventivo.DomicilioId);
        Assert.AreEqual(NivelDeRisco.Perigo, preventivo.Nivel);
        Assert.IsFalse(cenario.Alertas.Alertas.Any(a => a.DomicilioId == comum.Id));
    }

    /// <summary>Com o Clima fora do ar, ou com um alerta já aberto na casa, nenhum preventivo é aberto.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ClimaForaOuCasaComAlertaNaoAbre()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();

        int semClima = await cenario.AvaliarPrevisoes().ExecutarAsync(CancellationToken.None);
        cenario.Clima.Riscos["SP-MIRANTE"] = new RiscoDaRegiao(NivelDeRisco.Perigo, NivelDeRisco.Perigo, 0.91, "regra");
        await cenario.MedirAsync(36, 60);
        int comAlerta = await cenario.AvaliarPrevisoes().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(0, semClima);
        Assert.AreEqual(0, comAlerta);
        Assert.AreEqual("DE_SENTINELA", cenario.Alertas.Alertas.Single().Tipo);
    }
}
