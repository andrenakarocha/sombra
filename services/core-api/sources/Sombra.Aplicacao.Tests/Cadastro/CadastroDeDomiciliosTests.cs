using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Aplicacao.Cadastro;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;
using Sombra.Aplicacao.Tests.Apoio;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Tests.Apoio;
using Sombra.Privacidade;

namespace Sombra.Aplicacao.Tests.Cadastro;

/// <summary>O cadastro da casa pela aplicação, com a PII sempre mascarada na saída.</summary>
[TestClass]
public sealed class CadastroDeDomiciliosTests
{
    private static readonly CancellationToken _semCancelar = CancellationToken.None;

    /// <summary>O cadastro completo: casa, morador, rede, rotina, Sentinela e monitoramento ligado.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task CadastroCompletoLigaOMonitoramento()
    {
        Cenario cenario = new();
        CadastroDeDomicilios cadastro = cenario.Cadastro();
        string cpf = DocumentosSinteticos.Cpf(7);
        string cns = DocumentosSinteticos.CartaoSus(7);

        DomicilioResposta casa = await cadastro.CadastrarAsync(NovaCasa(), _semCancelar);
        MoradorResposta morador = await cadastro.AdicionarMoradorAsync(
            casa.Id,
            new NovoMoradorDto { Nome = "Lurdes A.", Idade = 82, Cpf = cpf, CartaoSus = cns, Telefone = "11 91234 5678", PerfilDeSaude = "toma losartana", FatoresDeRisco = ["uso_de_diuretico"] },
            revelarContatos: true,
            _semCancelar);
        IReadOnlyList<ContatoResposta> rede = await cadastro.DefinirRedeDeCuidadoAsync(
            casa.Id,
            new RedeDeCuidadoDto { Contatos = [new ContatoDto { Nome = "Ana", Telefone = "(11) 98765-4321", Papel = PapelDoContato.Familiar }] },
            revelarContatos: true,
            _semCancelar);
        RotinaResposta rotina = await cadastro.DefinirRotinaAsync(casa.Id, Rotina(ativo: true, "09:00"), _semCancelar);
        SentinelaVinculadaResposta sentinela = await cadastro.VincularSentinelaAsync(casa.Id, Sentinela(Cenario.DeviceId), _semCancelar);
        DomicilioResposta monitorada = await cadastro.AtivarMonitoramentoAsync(casa.Id, _semCancelar);

        Assert.AreEqual("Santana", casa.Bairro);
        Assert.AreEqual(82, morador.Idade);
        Assert.AreEqual($"***.{cpf[3..6]}.***-{cpf[9..]}", morador.Cpf);
        Assert.AreEqual($"*** **** **** {cns[^4..]}", morador.CartaoSus);
        Assert.AreEqual("[DADO DE SAÚDE PROTEGIDO]", morador.PerfilDeSaude);
        string[] fatoresEsperados = ["IDADE_AVANCADA", "USO_DE_DIURETICO"];
        CollectionAssert.AreEqual(fatoresEsperados, morador.FatoresDeRisco.ToArray());
        Assert.AreEqual("(11) 98765-4321", rede.Single().Telefone);
        Assert.AreEqual($"{casa.Id:N}-1", rede.Single().Id);
        Assert.AreEqual(new CheckInDiarioResposta(true, "09:00"), rotina.CheckInDiario);
        Assert.IsFalse(sentinela.Online);
        Assert.MatchesRegex("^[0-9a-f]{64}$", sentinela.Segredo);
        Assert.AreEqual("(11) 91234-5678", morador.Telefone);
        Assert.IsTrue(monitorada.Monitorado);
        Assert.AreEqual(6, cenario.UnidadeDeTrabalho.Gravacoes);
    }

    /// <summary>As máscaras seguem os formatos da spec 07.</summary>
    [TestMethod]
    public void MascarasSeguemASpec07()
    {
        Assert.AreEqual("***.456.***-00", MascaradorDePii.MascararCpf("12345678900"));
        Assert.AreEqual("*** **** **** 1234", MascaradorDePii.MascararCartaoSus("700000000001234"));
        Assert.AreEqual("(11) *****-4321", MascaradorDePii.MascararTelefone("11987654321"));
        Assert.AreEqual("(11) *****-4321", MascaradorDePii.MascararTelefone("1132104321"));
    }

    /// <summary>A visão da casa junta a última leitura, a Sentinela, o último check-in e o alerta que pede resposta.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task VisaoDaCasaJuntaOQueAconteceuPorUltimo()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        cenario.Alertas.Adicionar(AlertaPreventivo.Abrir(casa.Id, NivelDeRisco.Perigo, Cenario.Inicio.AddHours(-12)));
        await cenario.CheckInAsync(casa.Id, OrigemDoCheckIn.App, SituacaoDoCheckIn.EstouBem);
        cenario.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await cenario.MedirAsync(36, 60);

        DomicilioDetalheResposta visao = await cenario.Cadastro().ObterAsync(casa.Id, _semCancelar);

        Assert.AreEqual(NivelDeRisco.Perigo, visao.NivelAtual);
        Assert.AreEqual(36, visao.UltimaLeitura?.TemperaturaC);
        Assert.AreEqual(TipoDoAlertaNoApp.DeSentinela, visao.AlertaAberto?.Tipo);
        Assert.AreEqual(Cenario.Inicio, visao.UltimoCheckIn);
        Assert.IsNotNull(visao.Sentinela);
        Assert.IsTrue(visao.Sentinela.Online);
        Assert.AreEqual(Cenario.DeviceId, visao.Sentinela.DeviceId);
    }

    /// <summary>As consultas e a remoção devolvem o estado atual da casa.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ConsultasERemocoes()
    {
        Cenario cenario = new();
        Domicilio casa = cenario.CasaMonitorada();
        CadastroDeDomicilios cadastro = cenario.Cadastro();

        MoradorResposta morador = (await cadastro.ListarMoradoresAsync(casa.Id, revelarContatos: false, _semCancelar)).Single();
        IReadOnlyList<ContatoResposta> rede = await cadastro.ObterRedeDeCuidadoAsync(casa.Id, revelarContatos: false, _semCancelar);
        RotinaResposta rotina = await cadastro.ObterRotinaAsync(casa.Id, _semCancelar);
        DomicilioResposta desligada = await cadastro.DesativarMonitoramentoAsync(casa.Id, _semCancelar);
        await cadastro.RemoverMoradorAsync(casa.Id, morador.Id, _semCancelar);
        await cadastro.RemoverAsync(casa.Id, _semCancelar);

        Assert.StartsWith("***.", morador.Cpf);
        CollectionAssert.AreEqual(new[] { PapelDoContato.Familiar, PapelDoContato.Vizinho }, rede.Select(c => c.Papel).ToArray());
        Assert.IsTrue(rede.All(c => c.Telefone.Contains("*****", StringComparison.Ordinal)));
        Assert.AreEqual(new CheckInDiarioResposta(false, "09:00"), rotina.CheckInDiario);
        Assert.IsTrue(rotina.LembreteDeAgua.Ativo);
        Assert.IsFalse(desligada.Monitorado);
        Assert.IsEmpty(casa.Moradores);
        Assert.IsEmpty(cenario.Domicilios.Casas);
    }

    /// <summary>Cada erro tem o código certo: 400 por campo, 404, 409 e 422 com a regra do domínio.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task ErrosDeCadastroTemOCodigoCerto()
    {
        Cenario cenario = new();
        cenario.CasaMonitorada();
        CadastroDeDomicilios cadastro = cenario.Cadastro();
        DomicilioResposta casa = await cadastro.CadastrarAsync(NovaCasa(), _semCancelar);

        ValidacaoException vazio = await Assert.ThrowsExactlyAsync<ValidacaoException>(() => cadastro.CadastrarAsync(new NovoDomicilioDto(), _semCancelar));
        ValidacaoException contato = await Assert.ThrowsExactlyAsync<ValidacaoException>(
            () => cadastro.DefinirRedeDeCuidadoAsync(casa.Id, new RedeDeCuidadoDto { Contatos = [new ContatoDto { Nome = "Ana" }] }, revelarContatos: true, _semCancelar));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => cadastro.ObterAsync(Guid.CreateVersion7(), _semCancelar));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => cadastro.RemoverMoradorAsync(casa.Id, Guid.CreateVersion7(), _semCancelar));
        await Assert.ThrowsExactlyAsync<ConflitoException>(() => cadastro.VincularSentinelaAsync(casa.Id, Sentinela(Cenario.DeviceId), _semCancelar));

        Assert.HasCount(8, vazio.Erros);
        Assert.IsTrue(contato.Erros.ContainsKey("contatos[0].telefone"));
        Assert.IsTrue(contato.Erros.ContainsKey("contatos[0].papel"));
        Assert.AreEqual("R12", (await RegraVioladaAsync(() => cadastro.DefinirRotinaAsync(casa.Id, Rotina(ativo: true, "23:00"), _semCancelar))).Regra);
        Assert.AreEqual("R3", (await RegraVioladaAsync(() => cadastro.AdicionarMoradorAsync(casa.Id, Morador(DocumentosSinteticos.Cpf(2), "DIABETES"), revelarContatos: true, _semCancelar))).Regra);
        Assert.AreEqual("R1", (await RegraVioladaAsync(() => cadastro.AdicionarMoradorAsync(casa.Id, Morador("123.456.789-00"), revelarContatos: true, _semCancelar))).Regra);
        Assert.AreEqual("R8", (await RegraVioladaAsync(() => cadastro.AtivarMonitoramentoAsync(casa.Id, _semCancelar))).Regra);
    }

    /// <summary>
    /// D17: o morador é editado como o app manda. Campo vazio mantém o que existe; CPF, Cartão SUS e telefone trocam
    /// quando vêm; a idade avançada pode ser declarada; o perfil em texto livre nunca volta.
    /// </summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task EdicaoDoMoradorComoOAppManda()
    {
        Cenario cenario = new();
        CadastroDeDomicilios cadastro = cenario.Cadastro();
        DomicilioResposta casa = await cadastro.CadastrarAsync(NovaCasa(), _semCancelar);
        MoradorResposta criado = await cadastro.AdicionarMoradorAsync(casa.Id, Morador(DocumentosSinteticos.Cpf(4)), revelarContatos: true, _semCancelar);
        string cpfNovo = DocumentosSinteticos.Cpf(5);

        MoradorResposta mantido = await cadastro.AtualizarMoradorAsync(
            casa.Id, criado.Id, new AtualizacaoDeMoradorDto { Nome = "Lurdes Aparecida", Idade = 76, FatoresDeRisco = ["IDADE_AVANCADA"] }, revelarContatos: true, _semCancelar);
        MoradorResposta trocado = await cadastro.AtualizarMoradorAsync(
            casa.Id,
            criado.Id,
            new AtualizacaoDeMoradorDto { Nome = "Lurdes Aparecida", Idade = 79, Cpf = cpfNovo, Telefone = "(11) 3456-7890", PerfilDeSaude = "hipertensão", FatoresDeRisco = [] },
            revelarContatos: false,
            _semCancelar);

        Assert.AreEqual("Lurdes Aparecida", mantido.Nome);
        Assert.AreEqual(criado.Cpf, mantido.Cpf);
        Assert.AreEqual(76, mantido.Idade);
        string[] soAIdadeDeclarada = ["IDADE_AVANCADA"];
        CollectionAssert.AreEqual(soAIdadeDeclarada, mantido.FatoresDeRisco.ToArray());
        Assert.AreEqual($"***.{cpfNovo[3..6]}.***-{cpfNovo[9..]}", trocado.Cpf);
        Assert.AreEqual(79, trocado.Idade);
        Assert.AreEqual("(11) *****-7890", trocado.Telefone);
        Assert.AreEqual("[DADO DE SAÚDE PROTEGIDO]", trocado.PerfilDeSaude);
        Assert.IsEmpty(trocado.FatoresDeRisco);
        Assert.AreEqual("hipertensão", cenario.Domicilios.Casas.Single().Moradores.Single().ObservacoesDeSaude);
    }

    /// <summary>O QR precisa trazer o código da Sentinela; sem Cartão SUS não há cadastro; a rotina desligada guarda o horário.</summary>
    /// <returns>O teste.</returns>
    [TestMethod]
    public async Task VinculoCartaoSusERotinaComoOApp()
    {
        Cenario cenario = new();
        CadastroDeDomicilios cadastro = cenario.Cadastro();
        DomicilioResposta casa = await cadastro.CadastrarAsync(NovaCasa(), _semCancelar);

        RegraVioladaException codigoErrado = await RegraVioladaAsync(
            () => cadastro.VincularSentinelaAsync(casa.Id, new NovaSentinelaDto { DeviceId = Cenario.DeviceId, CodigoDeVinculo = "AAAAAA" }, _semCancelar));
        ValidacaoException semCartao = await Assert.ThrowsExactlyAsync<ValidacaoException>(
            () => cadastro.AdicionarMoradorAsync(casa.Id, Morador(DocumentosSinteticos.Cpf(6)) with { CartaoSus = string.Empty }, revelarContatos: true, _semCancelar));
        await cadastro.DefinirRotinaAsync(casa.Id, Rotina(ativo: false, "23:30"), _semCancelar);
        RotinaResposta rotina = await cadastro.ObterRotinaAsync(casa.Id, _semCancelar);

        Assert.AreEqual("VINCULO", codigoErrado.Regra);
        Assert.IsTrue(semCartao.Erros.ContainsKey("cartaoSus"));
        Assert.AreEqual(new CheckInDiarioResposta(false, "23:30"), rotina.CheckInDiario);
    }

    private static NovoDomicilioDto NovaCasa() => new()
    {
        Apelido = "Casa da Dona Lurdes",
        Logradouro = "Rua das Flores",
        Numero = "12",
        Bairro = "Santana",
        Cidade = "São Paulo",
        Uf = "SP",
        Cep = "02012-000",
        RegiaoClimatica = "SP-MIRANTE",
    };

    private static NovoMoradorDto Morador(string cpf, params string[] fatores)
        => new() { Nome = "Lurdes A.", Idade = 76, Cpf = cpf, CartaoSus = DocumentosSinteticos.CartaoSus(1), FatoresDeRisco = fatores };

    private static RotinaDto Rotina(bool ativo, string horario)
        => new() { CheckInDiario = new CheckInDiarioDto { Ativo = ativo, Horario = horario }, LembreteDeAgua = new LembreteDeAguaDto { Ativo = true } };

    private static NovaSentinelaDto Sentinela(string deviceId)
        => new() { DeviceId = deviceId, CodigoDeVinculo = CodigosDeVinculoDeTeste.Para(deviceId) };

    private static Task<RegraVioladaException> RegraVioladaAsync(Func<Task> acao) => Assert.ThrowsExactlyAsync<RegraVioladaException>(acao);
}
