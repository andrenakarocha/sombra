using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Alertas;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Tests.Apoio;

namespace Sombra.Infraestrutura.Tests;

/// <summary>Os casos de uso com os repositórios de verdade: cada passo é uma requisição nova, com contexto novo.</summary>
[TestClass]
public sealed class FluxoDeAlertaTests
{
    private static readonly CancellationToken _semCancelar = CancellationToken.None;

    /// <summary>Caminho B na casa da Dona Lurdes do seed: filha, vizinha, rede toda; depois o "estou bem" encerra.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CaminhoBAtravessaRequisicoes()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        RelogioFixo relogio = new(Casas.Agora);
        NotificadorGravado notificador = new();

        await using (SombraDbContext db = criar())
        {
            LeituraRegistradaResposta leitura = await new RegistrarLeitura(new DomicilioRepository(db), new AlertaRepository(db), new LeituraRepository(db), notificador, db)
                .ExecutarAsync(new NovaLeituraDto { DeviceId = "snt-7f3a9c", Momento = relogio.Agora, TemperaturaC = 36, UmidadePct = 60 }, _semCancelar);
            Assert.AreEqual("DE_SENTINELA", leitura.Alerta?.Tipo);
        }

        int[] subidas = new int[3];
        for (int i = 0; i < subidas.Length; i++)
        {
            relogio.Avancar(TimeSpan.FromMinutes(i == 0 ? 15 : 10));
            await using SombraDbContext db = criar();
            subidas[i] = await new EscalonarAlertas(new DomicilioRepository(db), new AlertaRepository(db), relogio, notificador, db).ExecutarAsync(_semCancelar);
        }

        CheckInResposta checkIn;
        await using (SombraDbContext db = criar())
        {
            checkIn = await new RegistrarCheckIn(new DomicilioRepository(db), new AlertaRepository(db), new LeituraRepository(db), new CheckInRepository(db), relogio, notificador, db)
                .ExecutarAsync(Casas.Lurdes, new NovoCheckInDto { Origem = OrigemDoCheckIn.BotaoSentinela, Situacao = SituacaoDoCheckIn.EstouBem }, _semCancelar);
        }

        int[] umPorVolta = [1, 1, 1];
        Destinatario[] daRede = [Destinatario.Familiar, Destinatario.Vizinho, Destinatario.Rede];
        CollectionAssert.AreEqual(umPorVolta, subidas);
        CollectionAssert.AreEqual(daRede, notificador.Avisos.Where(a => a.Destinatario != Destinatario.Morador).Select(a => a.Destinatario).ToArray());
        Assert.IsNotNull(checkIn.Alerta);
        Assert.AreEqual(EstadoDoAlerta.Resolvido, checkIn.Alerta.Estado);
        await using (SombraDbContext db = criar())
        {
            Assert.IsEmpty(await new AlertaRepository(db).ListarAbertosAsync(_semCancelar));
        }
    }
}
