using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Geo;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Clima;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Infraestrutura.Tests.Apoio;

namespace Sombra.Infraestrutura.Tests;

/// <summary>As consultas LINQ do painel, traduzidas para SQL e conferidas no PostgreSQL com o seed da demonstração.</summary>
[TestClass]
public sealed class ConsultasDoPainelTests
{
    private static readonly CancellationToken _semCancelar = CancellationToken.None;

    /// <summary>A lista de casas filtra pelo nível da última leitura e pelo bairro, pagina e diz quem está online.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ListaDeCasasFiltraEPagina()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        NivelDeRisco nivelDaLurdes = await MedirAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora, 36, 60);
        await MedirAsync(criar, Casas.Joaquim, "snt-2b81d4", Casas.Agora.AddHours(-1), 24, 50);
        await using SombraDbContext db = criar();
        ConsultasDoPainel consultas = new(db);

        Paginado<DomicilioDetalheResposta> emPerigo = await consultas.ListarDomiciliosAsync(new FiltroDeDomiciliosDto { Nivel = nivelDaLurdes }, null, Casas.Agora, _semCancelar);
        Paginado<DomicilioDetalheResposta> vilaMaria = await consultas.ListarDomiciliosAsync(new FiltroDeDomiciliosDto { Bairro = "Vila Maria" }, null, Casas.Agora, _semCancelar);
        Paginado<DomicilioDetalheResposta> segunda = await consultas.ListarDomiciliosAsync(new FiltroDeDomiciliosDto { Pagina = 2, Tamanho = 2 }, null, Casas.Agora, _semCancelar);

        Assert.AreEqual(NivelDeRisco.Perigo, nivelDaLurdes);
        Assert.AreEqual(1, emPerigo.Total);
        Assert.AreEqual("Casa da Dona Lurdes", emPerigo.Itens.Single().Apelido);
        Assert.IsTrue(emPerigo.Itens.Single().Sentinela?.Online);
        Assert.AreEqual(36, emPerigo.Itens.Single().UltimaLeitura?.TemperaturaC);
        DomicilioDetalheResposta joaquim = vilaMaria.Itens.Single();
        Assert.AreEqual(NivelDeRisco.Normal, joaquim.NivelAtual);
        Assert.IsFalse(joaquim.Sentinela?.Online);
        Assert.AreEqual(3, segunda.Total);
        Assert.AreEqual("Casa do Seu Joaquim", segunda.Itens.Single().Apelido);

        Paginado<DomicilioDetalheResposta> doCuidador = await consultas.ListarDomiciliosAsync(new FiltroDeDomiciliosDto(), [Casas.Joaquim], Casas.Agora, _semCancelar);
        Assert.AreEqual(Casas.Joaquim, doCuidador.Itens.Single().Id);
    }

    /// <summary>Alertas e leituras: filtros, período fechado embaixo e aberto em cima, do mais recente ao mais antigo.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task AlertasELeiturasFiltramEOrdenam()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await MedirAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora.AddHours(-2), 30, 50);
        await MedirAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora.AddHours(-1), 33, 45);
        await MedirAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora, 36, 60);
        await GravarAlertasAsync(
            criar,
            AlertaDePedidoDeAjuda.Abrir(Casas.Lurdes, NivelDeRisco.Normal, Casas.Agora.AddDays(-1)),
            AlertaPreventivo.Abrir(Casas.Lurdes, NivelDeRisco.Perigo, Casas.Agora.AddHours(-3)));
        await using SombraDbContext db = criar();
        ConsultasDoPainel consultas = new(db);

        Paginado<LeituraResposta> leituras = await consultas.ListarLeiturasAsync(
            Casas.Lurdes,
            new FiltroDeLeiturasDto { De = Casas.Agora.AddMinutes(-90), Ate = Casas.Agora },
            _semCancelar);
        Paginado<AlertaResposta> todos = await consultas.ListarAlertasAsync(new FiltroDeAlertasDto(), null, _semCancelar);
        Paginado<AlertaResposta> informativos = await consultas.ListarAlertasAsync(new FiltroDeAlertasDto { Estado = EstadoDoAlertaNoApp.AguardandoCheckin }, null, _semCancelar);

        Assert.AreEqual(1, leituras.Total);
        Assert.AreEqual(Casas.Agora.AddHours(-1), leituras.Itens.Single().Momento);
        Assert.AreEqual(33, leituras.Itens.Single().TemperaturaC);
        TipoDoAlertaNoApp[] tipos = [TipoDoAlertaNoApp.Preventivo, TipoDoAlertaNoApp.DeSentinela];
        CollectionAssert.AreEqual(tipos, todos.Itens.Select(a => a.Tipo).ToArray());
        Assert.IsTrue(todos.Itens.All(a => a.ApelidoDaCasa == "Casa da Dona Lurdes"));
        AlertaResposta preventivo = informativos.Itens.Single();
        Assert.AreEqual(TipoDoAlertaNoApp.Preventivo, preventivo.Tipo);
        Assert.AreEqual(preventivo.AbertoEm + AlertaPreventivo.Validade, preventivo.Prazo);
        Assert.AreEqual("Previsão de calor perigoso nas próximas 24 horas.", preventivo.LinhaDoTempo.Single().Descricao);
    }

    /// <summary>O resumo do painel: agregações por nível, degrau e bairro, mediana até o check-in e Sentinelas offline.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ResumoDoPainelAgregaNoBanco()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        Leitura daLurdes = await LeituraAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora.AddHours(-2));
        Leitura doJoaquim = await LeituraAsync(criar, Casas.Joaquim, "snt-2b81d4", Casas.Agora.AddHours(-1));
        await MedirAsync(criar, Casas.Lurdes, "snt-7f3a9c", Casas.Agora, 24, 50);

        AlertaDeSentinela respondido = AlertaDeSentinela.Abrir(Casas.Lurdes, daLurdes);
        respondido.ConfirmarEstouBem(daLurdes.Momento.AddMinutes(6));
        AlertaDeSentinela redeToda = AlertaDeSentinela.Abrir(Casas.Joaquim, doJoaquim);
        await using (SombraDbContext db = criar())
        {
            Domicilio? joaquim = await new DomicilioRepository(db).ObterAsync(Casas.Joaquim, _semCancelar);
            Assert.IsNotNull(joaquim);
            redeToda.Escalonar(doJoaquim.Momento.AddMinutes(35), joaquim.RedeDeCuidado);
        }

        redeToda.Reconhecer(AcaoDoCuidador.ElaEstaBem, doJoaquim.Momento.AddMinutes(40));
        await GravarAlertasAsync(
            criar,
            respondido,
            redeToda,
            AlertaDePedidoDeAjuda.Abrir(Casas.Joaquim, NivelDeRisco.Normal, Casas.Agora),
            AlertaPreventivo.Abrir(Casas.Benedita, NivelDeRisco.Perigo, Casas.Agora.AddHours(-3)));

        await using SombraDbContext leitura = criar();
        PainelResumoResposta resumo = await new ConsultasDoPainel(leitura).ObterResumoAsync(Casas.Agora, _semCancelar);

        Assert.AreEqual(2, resumo.CasasMonitoradas);
        Assert.AreEqual(2, resumo.AlertasAtivos);
        Assert.AreEqual(1, resumo.AlertasAtivosPorNivel[NivelDeRisco.Normal]);
        Assert.AreEqual(1, resumo.AlertasAtivosPorNivel[NivelDeRisco.Perigo]);
        Assert.AreEqual(23.0, resumo.TempoMedianoAteCheckInMin);
        Assert.AreEqual(50.0, resumo.AlertasResolvidosSemUltimoDegrauPct);
        Assert.AreEqual(1, resumo.AlarmesDesnecessariosSemana);
        CollectionAssert.AreEquivalent(
            new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 2 }.ToArray(),
            resumo.EscalonamentosHojePorDegrau.ToArray());
        Assert.AreEqual(1, resumo.SentinelasOffline);
        CollectionAssert.AreEquivalent(
            new Dictionary<string, int> { ["Santana"] = 1, ["Vila Maria"] = 2, ["Tucuruvi"] = 1 }.ToArray(),
            resumo.AlertasDaSemanaPorBairro.ToArray());
    }

    private static async Task<NivelDeRisco> MedirAsync(Func<SombraDbContext> criar, Guid casaId, string deviceId, DateTimeOffset momento, double temperatura, double umidade)
    {
        await using SombraDbContext db = criar();
        Domicilio? casa = await new DomicilioRepository(db).ObterAsync(casaId, _semCancelar);
        Assert.IsNotNull(casa);
        Leitura leitura = casa.RegistrarLeitura(deviceId, momento, Temperatura.Criar(temperatura), UmidadeRelativa.Criar(umidade));
        new LeituraRepository(db).Adicionar(casaId, leitura);
        await db.SalvarAsync(_semCancelar);
        return leitura.Nivel;
    }

    private static async Task<Leitura> LeituraAsync(Func<SombraDbContext> criar, Guid casaId, string deviceId, DateTimeOffset momento)
    {
        await MedirAsync(criar, casaId, deviceId, momento, 36, 60);
        await using SombraDbContext db = criar();
        Leitura? leitura = await new LeituraRepository(db).ObterUltimaAsync(casaId, _semCancelar);
        Assert.IsNotNull(leitura);
        return leitura;
    }

    /// <summary>
    /// D17: o farol de privacidade da casa conta só os eventos de segurança das Sentinelas dela, na última semana; os
    /// pontos de exemplo saem do mais perto ao mais longe, dentro do raio, com a fonte rotulada.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PrivacidadeDaCasaEPontosDeExemplo()
    {
        Func<SombraDbContext> criar = await BancoDeTeste.NovoBancoAsync();
        await using (SombraDbContext db = criar())
        {
            db.EventosDeSeguranca.AddRange(
                EventoDeSeguranca.Registrar(Casas.Agora.AddHours(-1), "MQTT_ASSINATURA_INVALIDA", "snt-7f3a9c", null, "telemetria seq 9"),
                EventoDeSeguranca.Registrar(Casas.Agora.AddHours(-2), "MQTT_REPLAY", "snt-7f3a9c", null, "seq 8 repetido"),
                EventoDeSeguranca.Registrar(Casas.Agora.AddDays(-9), "MQTT_REPLAY", "snt-7f3a9c", null, "seq 1 repetido"),
                EventoDeSeguranca.Registrar(Casas.Agora.AddHours(-1), "MQTT_SENTINELA_DESCONHECIDA", "snt-999999", null, "Sentinela sem casa"));
            await db.SaveChangesAsync(_semCancelar);
        }

        await using SombraDbContext leitura = criar();
        ConsultasDoPainel consultas = new(leitura);
        PrivacidadeDaCasaResposta daLurdes = await consultas.PrivacidadeDaCasaAsync(Casas.Lurdes, Casas.Agora, _semCancelar);
        PrivacidadeDaCasaResposta doJoaquim = await consultas.PrivacidadeDaCasaAsync(Casas.Joaquim, Casas.Agora, _semCancelar);
        IReadOnlyList<PontoDeResfriamentoResposta> pontos = await new PontosDeResfriamentoDeExemplo().ProximosAsync(PontoGeografico.Criar(-23.4964, -46.6200), 3, _semCancelar);

        Assert.AreEqual(FarolDePrivacidade.Amarelo, daLurdes.Farol);
        Assert.HasCount(2, daLurdes.Mensagens);
        Assert.AreEqual(FarolDePrivacidade.Verde, doJoaquim.Farol);
        double[] distancias = [0.8, 1.2, 2.0, 2.5];
        CollectionAssert.AreEqual(distancias, pontos.Select(p => p.DistanciaKm).ToArray());
        Assert.IsTrue(pontos.All(p => p.Nome.EndsWith("(exemplo)", StringComparison.Ordinal) && p.Fonte == PontosDeResfriamentoDeExemplo.Fonte));
    }

    private static async Task GravarAlertasAsync(Func<SombraDbContext> criar, params AlertaDeCalor[] alertas)
    {
        await using SombraDbContext db = criar();
        AlertaRepository repositorio = new(db);
        foreach (AlertaDeCalor alerta in alertas)
        {
            repositorio.Adicionar(alerta);
        }

        await db.SalvarAsync(_semCancelar);
    }
}
