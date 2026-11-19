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

    /// <summary>
    /// DA6: o pico real de nov/2023 (35,47 °C, CAUTELA_EXTREMA pela NWS) é perigo para quem tem 85 anos e cardiopatia
    /// (limite pessoal de 34,94 °C), mas não para quem tem 70 anos; sem o pico, vale o nível da região.
    /// </summary>
    [TestMethod]
    public void PrevisaoDaCasaUsaOLimitePessoalDoMoradorMaisVulneravel()
    {
        DateOnly hoje = DateOnly.FromDateTime(_agora.UtcDateTime);
        Domicilio comum = Fabricas.Casa();
        comum.AdicionarMorador(Fabricas.Morador(70, semente: 1));
        Domicilio vulneravel = Fabricas.Casa();
        vulneravel.AdicionarMorador(Fabricas.Morador(85, 2, new Cardiopatia()));

        Assert.AreEqual(NivelDeRisco.CautelaExtrema, comum.NivelPrevistoParaACasa(NivelDeRisco.CautelaExtrema, 35.47, hoje));
        Assert.AreEqual(NivelDeRisco.Perigo, vulneravel.NivelPrevistoParaACasa(NivelDeRisco.CautelaExtrema, 35.47, hoje));
        Assert.AreEqual(NivelDeRisco.CautelaExtrema, vulneravel.NivelPrevistoParaACasa(NivelDeRisco.CautelaExtrema, null, hoje));
        Assert.AreEqual(NivelDeRisco.Perigo, comum.NivelPrevistoParaACasa(NivelDeRisco.Perigo, 30.0, hoje), "o nível da região nunca baixa");
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

    /// <summary>O cadastro pode ser corrigido (apelido e endereço), com a mesma regra do apelido.</summary>
    [TestMethod]
    public void AtualizaApelidoEEndereco()
    {
        Domicilio casa = Fabricas.Casa();
        Endereco novo = Endereco.Criar("Rua Nova", "7", "Tucuruvi", "São Paulo", "SP", "02306000");

        casa.AtualizarCadastro(" Casa da Lurdes ", novo);

        Assert.AreEqual("Casa da Lurdes", casa.Apelido);
        Assert.AreEqual(novo, casa.Endereco);
        Assert.AreEqual("SP-MIRANTE", casa.RegiaoClimatica);
        Assert.ThrowsExactly<RegraVioladaException>(() => casa.AtualizarCadastro(" ", novo));
    }

    /// <summary>Regra R10: a Sentinela nasce com um segredo de 32 bytes e só aceita seq crescente dentro da janela de 300 s.</summary>
    [TestMethod]
    public void SentinelaTemSegredoEAntiReplay()
    {
        Sentinela sentinela = Fabricas.Casa().VincularSentinela("snt-7f3a9c");
        Assert.MatchesRegex("^[0-9a-f]{64}$", sentinela.SegredoHex);
        Assert.AreNotEqual(sentinela.SegredoHex, Sentinela.Criar("snt-7f3a9c").SegredoHex);

        Assert.AreEqual(RecusaDaMensagem.Nenhuma, sentinela.ReceberMensagem(5, _agora, _agora.AddSeconds(2)));
        Assert.AreEqual(RecusaDaMensagem.Repetida, sentinela.ReceberMensagem(5, _agora, _agora));
        Assert.AreEqual(RecusaDaMensagem.ForaDaJanela, sentinela.ReceberMensagem(6, _agora.AddSeconds(-301), _agora));
        Assert.AreEqual(5, sentinela.UltimoSeqRecebido);
        Assert.AreEqual(_agora, sentinela.UltimoContato);
        long primeiro = sentinela.ProximoSeqDeComando(_agora);
        Assert.AreEqual(_agora.ToUnixTimeSeconds(), primeiro);
        Assert.AreEqual(primeiro + 1, sentinela.ProximoSeqDeComando(_agora));

        sentinela.DefinirSegredo(new string('A', 64));
        Assert.AreEqual(new string('a', 64), sentinela.SegredoHex);
        Assert.AreEqual("R10", Assert.ThrowsExactly<RegraVioladaException>(() => sentinela.DefinirSegredo("curto")).Regra);
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

    /// <summary>Regra R1 na edição: trocar o CPF de um morador para o de outro morador da casa é recusado.</summary>
    [TestMethod]
    public void TrocaDeCpfNaoRepeteNaCasa()
    {
        Domicilio casa = Fabricas.Casa();
        Morador primeiro = Fabricas.Morador(78, semente: 1);
        Morador segundo = Fabricas.Morador(80, semente: 2);
        casa.AdicionarMorador(primeiro);
        casa.AdicionarMorador(segundo);

        Assert.AreEqual("R1", Assert.ThrowsExactly<RegraVioladaException>(() => casa.TrocarCpfDoMorador(segundo.Id, primeiro.Cpf)).Regra);
        casa.TrocarCpfDoMorador(segundo.Id, Cpf.Criar(DocumentosSinteticos.Cpf(9)));
        Assert.AreEqual(Cpf.Criar(DocumentosSinteticos.Cpf(9)), segundo.Cpf);
        casa.TrocarCpfDoMorador(primeiro.Id, primeiro.Cpf);
    }
}
