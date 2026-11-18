using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Arredores;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Geo;
using Sombra.Dominio.Pessoas;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Risco;
using Sombra.Privacidade.Traducao;

namespace Sombra.Aplicacao.Tests.Arredores;

/// <summary>O farol de privacidade da casa e os pontos de resfriamento, como o app lê (D17).</summary>
[TestClass]
public sealed class ArredoresTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// O farol é o Risco Cidadão dos eventos da casa: nada recente é verde; uma ou duas mensagens recusadas da Sentinela
    /// (credencial, alta) é amarelo; seis é vermelho; só as cinco últimas mensagens.
    /// </summary>
    [TestMethod]
    public void FarolEhORiscoCidadaoDosEventosDaCasa()
    {
        (string, EventoDeRisco, TraducaoDeRisco?)[] antigo = [Mqtt(_agora.AddDays(-8), "MQTT_REPLAY")];
        (string, EventoDeRisco, TraducaoDeRisco?)[] dois = [Mqtt(_agora.AddHours(-2), "MQTT_REPLAY"), Mqtt(_agora.AddHours(-1), "MQTT_ASSINATURA_INVALIDA")];
        (string, EventoDeRisco, TraducaoDeRisco?)[] seis = [.. Enumerable.Range(1, 6).Select(h => Mqtt(_agora.AddHours(-h), "OUTRO_TIPO"))];

        PrivacidadeDaCasaResposta verde = PrivacidadeDaCasaResposta.De(antigo, _agora);
        PrivacidadeDaCasaResposta amarelo = PrivacidadeDaCasaResposta.De(dois, _agora);
        PrivacidadeDaCasaResposta vermelho = PrivacidadeDaCasaResposta.De(seis, _agora);

        Assert.AreEqual(FarolDePrivacidade.Verde, verde.Farol);
        Assert.IsEmpty(verde.Mensagens);
        Assert.AreEqual(FarolDePrivacidade.Amarelo, amarelo.Farol);
        Assert.AreEqual(_agora.AddHours(-1), amarelo.Mensagens[0].Momento);
        Assert.StartsWith("Chegou uma mensagem com assinatura errada", amarelo.Mensagens[0].MensagemCidadao);
        Assert.IsTrue(amarelo.Mensagens.All(m => m.Fonte == "template"));
        Assert.AreEqual(FarolDePrivacidade.Vermelho, vermelho.Farol);
        Assert.HasCount(PrivacidadeDaCasaResposta.MaximoDeMensagens, vermelho.Mensagens);
        Assert.Contains("Nenhum dado pessoal saiu.", vermelho.Mensagens[0].MensagemCidadao);
    }

    /// <summary>A tradução da IA, quando existe, é a mensagem; a fonte diz de onde veio.</summary>
    [TestMethod]
    public void TraducaoDaIaViraAMensagem()
    {
        TraducaoDeRisco ia = new("Alguém tentou ver a saúde da casa, e o Sombra não deixou. Está tudo protegido.", "g", "t", TraducaoDeRisco.FonteIa);

        MensagemDePrivacidadeResposta mensagem = PrivacidadeDaCasaResposta.De(
            [("ACESSO_NEGADO", new EventoDeRisco(_agora.AddMinutes(-1), CategoriaDePii.Saude, SeveridadeDePii.Alta, Bloqueado: true), ia)],
            _agora).Mensagens.Single();

        Assert.AreEqual(ia.Cidadao, mensagem.MensagemCidadao);
        Assert.AreEqual("IA", mensagem.Fonte);
    }

    /// <summary>As mensagens dos guardas de privacidade dizem a família do dado, sem o dado e sem jargão.</summary>
    [TestMethod]
    public void MensagensDosGuardasDizemAFamilia()
    {
        PrivacidadeDaCasaResposta privacidade = PrivacidadeDaCasaResposta.De(
            [("ACESSO_NEGADO", new EventoDeRisco(_agora.AddMinutes(-1), CategoriaDePii.Saude, SeveridadeDePii.Alta, Bloqueado: true), null)],
            _agora);

        Assert.AreEqual(FarolDePrivacidade.Amarelo, privacidade.Farol);
        Assert.AreEqual("Alguém tentou ver um dado de saúde da casa sem permissão, e o Sombra bloqueou.", privacidade.Mensagens.Single().MensagemCidadao);
    }

    private static (string, EventoDeRisco, TraducaoDeRisco?) Mqtt(DateTimeOffset momento, string tipo)
        => (tipo, new EventoDeRisco(momento, CategoriaDePii.Credencial, SeveridadeDePii.Alta, Bloqueado: true), null);

    /// <summary>Os pontos saem da referência da região da casa; região sem referência dá lista vazia; casa inexistente, 404.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task PontosPartemDaEstacaoDaRegiao()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        Domicilio semReferencia = Domicilio.Cadastrar("Outra casa", Endereco.Criar("Rua B", "2", "Centro", "Campinas", "SP", "13010-000"), "SP-CAMPINAS");
        cenario.Domicilios.Casas.Add(semReferencia);
        PontosFalsos pontos = new();
        PontosDeResfriamentoDaCasa consulta = new(cenario.Domicilios, pontos);

        IReadOnlyList<PontoDeResfriamentoResposta> daCasa = await consulta.ExecutarAsync(casa.Id, new FiltroDePontosDto { RaioKm = 2 }, CancellationToken.None);
        IReadOnlyList<PontoDeResfriamentoResposta> vazio = await consulta.ExecutarAsync(semReferencia.Id, new FiltroDePontosDto(), CancellationToken.None);

        Assert.HasCount(1, daCasa);
        Assert.AreEqual(-23.4964, pontos.Origem?.Latitude);
        Assert.AreEqual(2.0, pontos.Raio);
        Assert.IsEmpty(vazio);
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => consulta.ExecutarAsync(Guid.CreateVersion7(), new FiltroDePontosDto(), CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ValidacaoException>(() => consulta.ExecutarAsync(casa.Id, new FiltroDePontosDto { RaioKm = 50 }, CancellationToken.None));
    }

    private sealed class PontosFalsos : IPontosDeResfriamento
    {
        internal PontoGeografico? Origem { get; private set; }

        internal double Raio { get; private set; }

        public Task<IReadOnlyList<PontoDeResfriamentoResposta>> ProximosAsync(PontoGeografico origem, double raioKm, CancellationToken cancellationToken)
        {
            Origem = origem;
            Raio = raioKm;
            IReadOnlyList<PontoDeResfriamentoResposta> um = [new("p1", "Biblioteca (exemplo)", "BIBLIOTECA", 0.8, new("09:00", "18:00"), true, "teste", -23.5, -46.62)];
            return Task.FromResult(um);
        }
    }
}
