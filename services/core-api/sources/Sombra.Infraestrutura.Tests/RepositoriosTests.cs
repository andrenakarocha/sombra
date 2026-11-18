using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Tests.Apoio;

namespace Sombra.Infraestrutura.Tests;

/// <summary>Os agregados vão ao PostgreSQL e voltam iguais, cada etapa num contexto novo, como numa requisição.</summary>
[TestClass]
public sealed class RepositoriosTests
{
    private static readonly CancellationToken _semCancelar = CancellationToken.None;

    /// <summary>A casa volta inteira; rede em outra ordem, rotina, morador removido e contato da Sentinela persistem.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CasaVaiEVoltaInteiraComAsMudancas()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        Domicilio casa = Casas.Monitorada("snt-0000a1");
        Guid moradorId = casa.Moradores[0].Id;
        await using (SombraDbContext db = criar())
        {
            new DomicilioRepository(db).Adicionar(casa);
            await db.SalvarAsync(_semCancelar);
        }

        await using (SombraDbContext db = criar())
        {
            Domicilio? lida = await new DomicilioRepository(db).ObterAsync(casa.Id, _semCancelar);
            Assert.IsNotNull(lida);
            Assert.AreEqual(casa.Endereco, lida.Endereco);
            Assert.AreEqual(casa.Rotina, lida.Rotina);
            CollectionAssert.AreEqual(casa.RedeDeCuidado.Contatos.ToArray(), lida.RedeDeCuidado.Contatos.ToArray());
            Morador morador = lida.Moradores.Single();
            Assert.AreEqual(casa.Moradores[0].Cpf, morador.Cpf);
            Assert.AreEqual(casa.Moradores[0].CartaoSus, morador.CartaoSus);
            Assert.AreEqual(casa.Moradores[0].PerfilDeSaude, morador.PerfilDeSaude);

            lida.DefinirRedeDeCuidado([.. lida.RedeDeCuidado.Contatos.Reverse()]);
            lida.DefinirRotina(Rotina.Criar(null, lembreteDeAguaAtivo: false));
            lida.Sentinelas[0].RegistrarContato(Casas.Agora.ToOffset(TimeSpan.FromHours(-3)));
            lida.DesativarMonitoramento();
            Assert.IsTrue(lida.RemoverMorador(moradorId));
            await db.SalvarAsync(_semCancelar);
        }

        await using (SombraDbContext db = criar())
        {
            DomicilioRepository casas = new(db);
            Domicilio? depois = await casas.ObterAsync(casa.Id, _semCancelar);
            Assert.IsNotNull(depois);
            Assert.AreEqual(PapelDoContato.Vizinho, depois.RedeDeCuidado.Contatos[0].Papel);
            Assert.IsNull(depois.Rotina.HorarioDoCheckInDiario);
            Assert.IsFalse(depois.Rotina.LembreteDeAguaAtivo);
            Assert.AreEqual(Casas.Agora, depois.Sentinelas[0].UltimoContato);
            Assert.IsFalse(depois.Monitorado);
            Assert.IsEmpty(depois.Moradores);
            Assert.IsFalse(await db.Set<Morador>().AnyAsync(m => m.Id == moradorId));
            Assert.AreSame(depois, await casas.ObterPorSentinelaAsync("snt-0000a1", _semCancelar));
        }
    }

    /// <summary>O alerta de Sentinela recarregado continua o escalonamento de onde parou; os três tipos voltam com o tipo certo.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AlertaRecarregadoContinuaOEscalonamento()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        Domicilio casa = Casas.Monitorada("snt-0000a2");
        Leitura leitura = casa.RegistrarLeitura("snt-0000a2", Casas.Agora, Temperatura.Criar(36), UmidadeRelativa.Criar(60));
        AlertaDeSentinela alerta = AlertaDeSentinela.Abrir(casa.Id, leitura);
        AlertaDePedidoDeAjuda encerrado = AlertaDePedidoDeAjuda.Abrir(casa.Id, NivelDeRisco.Normal, Casas.Agora.AddDays(-1));
        encerrado.ConfirmarEstouBem(Casas.Agora.AddDays(-1).AddMinutes(2));
        await using (SombraDbContext db = criar())
        {
            new DomicilioRepository(db).Adicionar(casa);
            AlertaRepository alertas = new(db);
            alertas.Adicionar(AlertaPreventivo.Abrir(casa.Id, NivelDeRisco.Perigo, Casas.Agora.AddHours(-12)));
            alertas.Adicionar(alerta);
            alertas.Adicionar(encerrado);
            await db.SalvarAsync(_semCancelar);
        }

        await EscalonarAsync(criar, casa.Id, alerta.Id, Casas.Agora.AddMinutes(15), DegrauDeEscalonamento.LembreteLeve);
        await EscalonarAsync(criar, casa.Id, alerta.Id, Casas.Agora.AddMinutes(24), DegrauDeEscalonamento.LembreteLeve);
        await EscalonarAsync(criar, casa.Id, alerta.Id, Casas.Agora.AddMinutes(25), DegrauDeEscalonamento.QuemEstaPerto);

        await using (SombraDbContext db = criar())
        {
            AlertaRepository alertas = new(db);
            AlertaDeCalor[] abertos = [.. await alertas.ListarAbertosDaCasaAsync(casa.Id, _semCancelar)];
            Assert.HasCount(2, abertos);
            Assert.IsInstanceOfType<AlertaPreventivo>(abertos[0]);
            Assert.IsInstanceOfType<AlertaDeSentinela>(abertos[1]);
            Assert.AreEqual(EstadoDoAlerta.Escalonado, abertos[1].Estado);
            Assert.IsInstanceOfType<AlertaDePedidoDeAjuda>(await alertas.ObterAsync(encerrado.Id, _semCancelar));
            Assert.HasCount(2, await alertas.ListarAbertosAsync(_semCancelar));
        }
    }

    /// <summary>Leituras e check-ins voltam do mais recente; a leitura recalcula o mesmo índice de calor e guarda o nível.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task LeiturasECheckInsVoltamDoMaisRecente()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        Domicilio casa = Casas.Monitorada("snt-0000a3");
        Leitura maisRecente = casa.RegistrarLeitura("snt-0000a3", Casas.Agora, Temperatura.Criar(36), UmidadeRelativa.Criar(60));
        await using (SombraDbContext db = criar())
        {
            new DomicilioRepository(db).Adicionar(casa);
            LeituraRepository leituras = new(db);
            leituras.Adicionar(casa.Id, casa.RegistrarLeitura("snt-0000a3", Casas.Agora.AddMinutes(-20), Temperatura.Criar(30), UmidadeRelativa.Criar(50)));
            leituras.Adicionar(casa.Id, maisRecente);
            leituras.Adicionar(casa.Id, casa.RegistrarLeitura("snt-0000a3", Casas.Agora.AddMinutes(-10), Temperatura.Criar(31), UmidadeRelativa.Criar(50)));
            CheckInRepository checkIns = new(db);
            checkIns.Adicionar(CheckIn.Registrar(casa.Id, Casas.Agora.AddMinutes(-30), OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem));
            checkIns.Adicionar(CheckIn.Registrar(casa.Id, Casas.Agora.AddMinutes(-5), OrigemDoCheckIn.BotaoSentinela, SituacaoDoCheckIn.EstouBem));
            await db.SalvarAsync(_semCancelar);
        }

        await using (SombraDbContext db = criar())
        {
            Leitura? ultima = await new LeituraRepository(db).ObterUltimaAsync(casa.Id, _semCancelar);
            CheckIn? ultimoCheckIn = await new CheckInRepository(db).ObterUltimoAsync(casa.Id, _semCancelar);

            Assert.IsNotNull(ultima);
            Assert.AreEqual(maisRecente.Id, ultima.Id);
            Assert.AreEqual(maisRecente.IndiceDeCalor, ultima.IndiceDeCalor);
            Assert.AreEqual(maisRecente.Nivel, ultima.Nivel);
            Assert.AreEqual(Casas.Agora.AddMinutes(-5), ultimoCheckIn?.Momento);
            Assert.AreEqual(OrigemDoCheckIn.BotaoSentinela, ultimoCheckIn?.Origem);
        }
    }

    private static async Task EscalonarAsync(Func<SombraDbContext> criar, Guid casaId, Guid alertaId, DateTimeOffset agora, DegrauDeEscalonamento esperado)
    {
        await using SombraDbContext db = criar();
        Domicilio? casa = await new DomicilioRepository(db).ObterAsync(casaId, _semCancelar);
        AlertaDeCalor? alerta = await new AlertaRepository(db).ObterAsync(alertaId, _semCancelar);
        Assert.IsNotNull(casa);
        Assert.IsNotNull(alerta);
        alerta.Escalonar(agora, casa.RedeDeCuidado);
        Assert.AreEqual(esperado, alerta.Degrau);
        await db.SalvarAsync(_semCancelar);
    }
}
