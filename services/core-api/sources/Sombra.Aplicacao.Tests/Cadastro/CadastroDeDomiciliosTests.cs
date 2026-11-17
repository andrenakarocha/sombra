using System;
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
            new NovoMoradorDto { Nome = "Lurdes A.", DataDeNascimento = new DateOnly(1944, 5, 2), Cpf = cpf, CartaoSus = cns, FatoresDeRisco = ["uso_de_diuretico"] },
            _semCancelar);
        RedeDeCuidadoResposta rede = await cadastro.DefinirRedeDeCuidadoAsync(
            casa.Id,
            new RedeDeCuidadoDto { Contatos = [new ContatoDto { Nome = "Ana", Telefone = "(11) 98765-4321", Papel = PapelDoContato.Familiar }] },
            _semCancelar);
        RotinaResposta rotina = await cadastro.DefinirRotinaAsync(casa.Id, new RotinaDto { HorarioDoCheckInDiario = new TimeOnly(9, 0), LembreteDeAguaAtivo = true }, _semCancelar);
        SentinelaResposta sentinela = await cadastro.VincularSentinelaAsync(casa.Id, new NovaSentinelaDto { DeviceId = Cenario.DeviceId }, _semCancelar);
        DomicilioResposta monitorada = await cadastro.AtivarMonitoramentoAsync(casa.Id, _semCancelar);

        Assert.AreEqual("Santana", casa.Bairro);
        Assert.AreEqual(82, morador.Idade);
        Assert.AreEqual($"***.{cpf[3..6]}.***-{cpf[9..]}", morador.Cpf);
        Assert.AreEqual($"*** **** **** {cns[^4..]}", morador.CartaoSus);
        Assert.AreEqual("[DADO DE SAÚDE PROTEGIDO]", morador.PerfilDeSaude);
        string[] fatoresEsperados = ["IDADE_AVANCADA", "USO_DE_DIURETICO"];
        CollectionAssert.AreEqual(fatoresEsperados, morador.FatoresDeRisco.ToArray());
        Assert.AreEqual("(11) *****-4321", rede.Contatos.Single().Telefone);
        Assert.AreEqual(new TimeOnly(9, 0), rotina.HorarioDoCheckInDiario);
        Assert.IsFalse(sentinela.Online);
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
        Assert.AreEqual("DE_SENTINELA", visao.AlertaAberto?.Tipo);
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

        MoradorResposta morador = (await cadastro.ListarMoradoresAsync(casa.Id, _semCancelar)).Single();
        RedeDeCuidadoResposta rede = await cadastro.ObterRedeDeCuidadoAsync(casa.Id, _semCancelar);
        RotinaResposta rotina = await cadastro.ObterRotinaAsync(casa.Id, _semCancelar);
        DomicilioResposta desligada = await cadastro.DesativarMonitoramentoAsync(casa.Id, _semCancelar);
        await cadastro.RemoverMoradorAsync(casa.Id, morador.Id, _semCancelar);
        await cadastro.RemoverAsync(casa.Id, _semCancelar);

        Assert.StartsWith("***.", morador.Cpf);
        CollectionAssert.AreEqual(new[] { PapelDoContato.Familiar, PapelDoContato.Vizinho }, rede.Contatos.Select(c => c.Papel).ToArray());
        Assert.IsNull(rotina.HorarioDoCheckInDiario);
        Assert.IsTrue(rotina.LembreteDeAguaAtivo);
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
            () => cadastro.DefinirRedeDeCuidadoAsync(casa.Id, new RedeDeCuidadoDto { Contatos = [new ContatoDto { Nome = "Ana" }] }, _semCancelar));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => cadastro.ObterAsync(Guid.CreateVersion7(), _semCancelar));
        await Assert.ThrowsExactlyAsync<RecursoNaoEncontradoException>(() => cadastro.RemoverMoradorAsync(casa.Id, Guid.CreateVersion7(), _semCancelar));
        await Assert.ThrowsExactlyAsync<ConflitoException>(() => cadastro.VincularSentinelaAsync(casa.Id, new NovaSentinelaDto { DeviceId = Cenario.DeviceId }, _semCancelar));

        Assert.HasCount(8, vazio.Erros);
        Assert.IsTrue(contato.Erros.ContainsKey("contatos[0].telefone"));
        Assert.IsTrue(contato.Erros.ContainsKey("contatos[0].papel"));
        Assert.AreEqual("R12", (await RegraVioladaAsync(() => cadastro.DefinirRotinaAsync(casa.Id, new RotinaDto { HorarioDoCheckInDiario = new TimeOnly(23, 0), LembreteDeAguaAtivo = true }, _semCancelar))).Regra);
        Assert.AreEqual("R3", (await RegraVioladaAsync(() => cadastro.AdicionarMoradorAsync(casa.Id, Morador(DocumentosSinteticos.Cpf(2), "DIABETES"), _semCancelar))).Regra);
        Assert.AreEqual("R1", (await RegraVioladaAsync(() => cadastro.AdicionarMoradorAsync(casa.Id, Morador("123.456.789-00"), _semCancelar))).Regra);
        Assert.AreEqual("R8", (await RegraVioladaAsync(() => cadastro.AtivarMonitoramentoAsync(casa.Id, _semCancelar))).Regra);
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
        => new() { Nome = "Lurdes A.", DataDeNascimento = new DateOnly(1950, 1, 1), Cpf = cpf, FatoresDeRisco = fatores };

    private static Task<RegraVioladaException> RegraVioladaAsync(Func<Task> acao) => Assert.ThrowsExactlyAsync<RegraVioladaException>(acao);
}
