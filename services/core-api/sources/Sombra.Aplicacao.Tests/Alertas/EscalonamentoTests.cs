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
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Tests.Alertas;

/// <summary>O escalonador em degraus (D13, caminho B) e a resposta do cuidador.</summary>
[TestClass]
public sealed class EscalonamentoTests
{
    /// <summary>Caminho B: sem resposta em perigo, a filha, depois a vizinha, depois a rede toda; a tela muda a cada subida.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoBSobeOsDegrausEAvisaCadaUm()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        await cenario.MedirAsync(36, 60);
        EscalonarAlertas escalonador = cenario.EscalonarAlertas();

        int[] subidas = new int[5];
        TimeSpan[] passos = [TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10), TimeSpan.FromHours(1)];
        for (int i = 0; i < passos.Length; i++)
        {
            cenario.Relogio.Avancar(passos[i]);
            subidas[i] = await escalonador.ExecutarAsync(CancellationToken.None);
        }

        int[] subidasEsperadas = [0, 1, 1, 1, 0];
        CollectionAssert.AreEqual(subidasEsperadas, subidas);
        Destinatario[] esperados =
        [
            Destinatario.Morador,
            Destinatario.Familiar, Destinatario.Morador,
            Destinatario.Vizinho, Destinatario.Morador,
            Destinatario.Rede, Destinatario.Morador,
        ];
        CollectionAssert.AreEqual(esperados, cenario.Notificador.Avisos.Select(a => a.Destinatario).ToArray());
        Assert.AreEqual("Ana", cenario.Notificador.Avisos[1].Contatos.Single().Nome);
        Assert.AreEqual("Cida (vizinha)", cenario.Notificador.Avisos[3].Contatos.Single().Nome);
        Assert.HasCount(2, cenario.Notificador.Avisos[5].Contatos);
        Assert.AreEqual("Sua família foi avisada. Aperte o botão verde se você está bem.", cenario.Notificador.Avisos[2].Mensagem);
    }

    /// <summary>Uma volta atrasada sobe de uma vez todos os degraus vencidos.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task VoltaAtrasadaSobeTodosOsDegrausVencidos()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        await cenario.MedirAsync(36, 60);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(40));

        int subidas = await cenario.EscalonarAlertas().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(3, subidas);
        Assert.AreEqual(DegrauDeEscalonamento.AlertaParaARede, cenario.Alertas.Alertas.Single().Degrau);
    }

    /// <summary>Regra R8: em cautela extrema, esquecer o "estou bem" nunca passa do lembrete leve.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CautelaExtremaParaNoLembreteLeve()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        await cenario.MedirAsync(33, 45);
        cenario.Relogio.Avancar(TimeSpan.FromHours(2));

        int subidas = await cenario.EscalonarAlertas().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(1, subidas);
        Assert.AreEqual(0, cenario.Notificador.Avisos.Count(a => a.Destinatario is Destinatario.Vizinho or Destinatario.Rede));
    }

    /// <summary>O preventivo que passou da validade é encerrado na volta, sem aviso.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PreventivoVencidoExpira()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        AlertaPreventivo preventivo = AlertaPreventivo.Abrir(casa.Id, NivelDeRisco.Perigo, Cenario.Inicio);
        cenario.Alertas.Adicionar(preventivo);
        cenario.Relogio.Avancar(AlertaPreventivo.Validade);

        await cenario.EscalonarAlertas().ExecutarAsync(CancellationToken.None);

        Assert.AreEqual(EstadoDoAlerta.Expirado, preventivo.Estado);
        Assert.IsEmpty(cenario.Notificador.Avisos);
    }

    /// <summary>"Estou indo" para o escalonamento; "ela está bem" encerra; alerta inexistente dá 404.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CuidadorParaOuEncerraOEscalonamento()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        LeituraRegistradaResposta leitura = await cenario.MedirAsync(36, 60);
        Assert.IsNotNull(leitura.Alerta);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(16));
        await cenario.EscalonarAlertas().ExecutarAsync(CancellationToken.None);

        AlertaResposta indo = await cenario.ReconhecerAlerta().ExecutarAsync(leitura.Alerta.Id, new ReconhecimentoDto { Acao = AcaoDoCuidador.EstouIndo }, CancellationToken.None);
        cenario.Relogio.Avancar(TimeSpan.FromHours(1));
        int subidasDepois = await cenario.EscalonarAlertas().ExecutarAsync(CancellationToken.None);
        AlertaResposta bem = await cenario.ReconhecerAlerta().ExecutarAsync(leitura.Alerta.Id, new ReconhecimentoDto { Acao = AcaoDoCuidador.ElaEstaBem }, CancellationToken.None);

        Assert.AreEqual(EstadoDoAlerta.Reconhecido, indo.Estado);
        Assert.AreEqual(0, subidasDepois);
        Assert.AreEqual(EstadoDoAlerta.Resolvido, bem.Estado);
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(
            () => cenario.ReconhecerAlerta().ExecutarAsync(Guid.CreateVersion7(), new ReconhecimentoDto { Acao = AcaoDoCuidador.VouLigar }, CancellationToken.None));
    }
}
