using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Risco;

namespace Sombra.Privacidade.Tests;

/// <summary>O cálculo do farol (🔎 da F2.5): pesos, desfecho, decaimento, janela, teto e cores.</summary>
[TestClass]
public sealed class RiscoCidadaoTests
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 18, 18, 0, 0, TimeSpan.Zero);

    /// <summary>Sem evento, verde e zero.</summary>
    [TestMethod]
    public void SemEventoEhVerde()
    {
        AvaliacaoDeRisco a = RiscoCidadao.Avaliar([], _agora);

        Assert.AreEqual(0, a.Pontuacao);
        Assert.AreEqual(FarolDeRisco.Verde, a.Farol);
        Assert.AreEqual(0, a.Eventos);
    }

    /// <summary>Saúde pesa mais que identificação, que pesa mais que contato (mesma severidade, mesmo momento).</summary>
    [TestMethod]
    public void SaudePesaMaisQueIdentificacaoQuePesaMaisQueContato()
    {
        double saude = RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude), _agora);
        double identificacao = RiscoCidadao.Pontos(Evento(CategoriaDePii.Identificacao), _agora);
        double contato = RiscoCidadao.Pontos(Evento(CategoriaDePii.Contato), _agora);

        Assert.IsGreaterThan(identificacao, saude);
        Assert.IsGreaterThan(contato, identificacao);
    }

    /// <summary>Um vazamento vale três bloqueios, e uma regra crítica vale mais que uma baixa.</summary>
    [TestMethod]
    public void VazadoEhPiorQueBloqueadoECriticaPiorQueBaixa()
    {
        Assert.AreEqual(3 * RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude), _agora), RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude, bloqueado: false), _agora), 1e-9);
        Assert.IsGreaterThan(
            RiscoCidadao.Pontos(Evento(CategoriaDePii.Credencial, SeveridadeDePii.Baixa), _agora),
            RiscoCidadao.Pontos(Evento(CategoriaDePii.Credencial, SeveridadeDePii.Critica), _agora));
    }

    /// <summary>Depois de uma meia-vida, o evento vale metade; depois da janela, nada; um evento no futuro não conta.</summary>
    [TestMethod]
    public void DecaiPelaMeiaVidaESaiDaJanela()
    {
        double fresco = RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude, idade: TimeSpan.Zero), _agora);

        Assert.AreEqual(fresco / 2, RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude, idade: RiscoCidadao.MeiaVida), _agora), 1e-9);
        Assert.AreEqual(0, RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude, idade: RiscoCidadao.Janela + TimeSpan.FromMinutes(1)), _agora));
        Assert.AreEqual(0, RiscoCidadao.Pontos(Evento(CategoriaDePii.Saude, idade: TimeSpan.FromMinutes(-5)), _agora));
    }

    /// <summary>
    /// A calibragem: um bloqueio de saúde recente acende o amarelo, dois continuam amarelos, três acendem o vermelho; um
    /// vazamento de saúde acende o vermelho sozinho; um bloqueio de contato sozinho fica verde.
    /// </summary>
    /// <param name="bloqueiosDeSaude">Quantos bloqueios de saúde.</param>
    /// <param name="vazamentosDeSaude">Quantos vazamentos de saúde.</param>
    /// <param name="bloqueiosDeContato">Quantos bloqueios de contato.</param>
    /// <param name="esperado">A cor.</param>
    [TestMethod]
    [DataRow(1, 0, 0, FarolDeRisco.Amarelo)]
    [DataRow(2, 0, 0, FarolDeRisco.Amarelo)]
    [DataRow(3, 0, 0, FarolDeRisco.Vermelho)]
    [DataRow(0, 1, 0, FarolDeRisco.Vermelho)]
    [DataRow(0, 0, 1, FarolDeRisco.Verde)]
    public void CalibragemDasCores(int bloqueiosDeSaude, int vazamentosDeSaude, int bloqueiosDeContato, FarolDeRisco esperado)
    {
        EventoDeRisco[] eventos =
        [
            .. Enumerable.Repeat(Evento(CategoriaDePii.Saude), bloqueiosDeSaude),
            .. Enumerable.Repeat(Evento(CategoriaDePii.Saude, bloqueado: false), vazamentosDeSaude),
            .. Enumerable.Repeat(Evento(CategoriaDePii.Contato, SeveridadeDePii.Media), bloqueiosDeContato),
        ];

        Assert.AreEqual(esperado, RiscoCidadao.Avaliar(eventos, _agora).Farol);
    }

    /// <summary>A pontuação tem teto de 100, no total e por família, e cada família tem a sua conta.</summary>
    [TestMethod]
    public void TetoDeCemEContaPorFamilia()
    {
        AvaliacaoDeRisco a = RiscoCidadao.Avaliar(
            [.. Enumerable.Repeat(Evento(CategoriaDePii.Saude, bloqueado: false), 5), Evento(CategoriaDePii.Contato, SeveridadeDePii.Media)],
            _agora);

        Assert.AreEqual(100, a.Pontuacao);
        Assert.AreEqual(100, a.PorCategoria[CategoriaDePii.Saude]);
        Assert.AreEqual(14.4, a.PorCategoria[CategoriaDePii.Contato]);
        Assert.AreEqual(6, a.Eventos);
    }

    /// <summary>Os limites das cores: 29,9 é verde, 30 é amarelo, 70 é vermelho.</summary>
    [TestMethod]
    public void LimitesDasCores()
    {
        Assert.AreEqual(FarolDeRisco.Verde, RiscoCidadao.FarolDe(29.9));
        Assert.AreEqual(FarolDeRisco.Amarelo, RiscoCidadao.FarolDe(30));
        Assert.AreEqual(FarolDeRisco.Amarelo, RiscoCidadao.FarolDe(69.9));
        Assert.AreEqual(FarolDeRisco.Vermelho, RiscoCidadao.FarolDe(70));
    }

    private static EventoDeRisco Evento(CategoriaDePii categoria, SeveridadeDePii severidade = SeveridadeDePii.Alta, bool bloqueado = true, TimeSpan? idade = null)
        => new(_agora - (idade ?? TimeSpan.FromMinutes(1)), categoria, severidade, bloqueado);
}
