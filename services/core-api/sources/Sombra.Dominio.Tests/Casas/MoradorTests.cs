using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;
using Sombra.Dominio.Tests.Apoio;

namespace Sombra.Dominio.Tests.Casas;

/// <summary>Regra R3: os fatores de risco do morador reduzem os limites térmicos dele.</summary>
[TestClass]
public sealed class MoradorTests
{
    /// <summary>A idade avançada entra sozinha a partir dos 80 anos, sem precisar ser declarada.</summary>
    [TestMethod]
    public void IdadeAvancadaVemDaDataDeNascimento()
    {
        Assert.IsFalse(Fabricas.Morador(79).FatoresDeRisco(Fabricas.Hoje).OfType<IdadeAvancada>().Any());
        Assert.IsTrue(Fabricas.Morador(80).FatoresDeRisco(Fabricas.Hoje).OfType<IdadeAvancada>().Any());
    }

    /// <summary>Cada fator reduz os limites, e a soma nunca passa da redução máxima.</summary>
    [TestMethod]
    public void FatoresReduzemOsLimitesAteOMaximo()
    {
        Morador semFatores = Fabricas.Morador(70);
        Morador comDiuretico = Fabricas.Morador(70, 2, new UsoDeDiuretico());
        Morador todos = Fabricas.Morador(85, 3, new Cardiopatia(), new UsoDeDiuretico());

        Assert.AreEqual(LimitesTermicos.Padrao, semFatores.LimitesTermicos(Fabricas.Hoje));
        Assert.AreEqual(LimitesTermicos.Padrao.Perigo - 1.5, comDiuretico.LimitesTermicos(Fabricas.Hoje).Perigo, 1e-9);
        Assert.AreEqual(LimitesTermicos.ComReducao(LimitesTermicos.ReducaoMaxima), todos.LimitesTermicos(Fabricas.Hoje));
    }

    /// <summary>Fatores repetidos contam uma vez, e a idade avançada não é declarável (vem da data).</summary>
    [TestMethod]
    public void PerfilIgnoraRepeticoesEIdadeDeclarada()
    {
        PerfilDeSaude perfil = PerfilDeSaude.Com([new Cardiopatia(), new Cardiopatia(), new IdadeAvancada()]);

        Assert.AreEqual(1, perfil.Fatores.Count);
        Assert.AreEqual(PerfilDeSaude.Com([new Cardiopatia()]), perfil);
        Assert.AreEqual("[DADO DE SAÚDE PROTEGIDO]", perfil.ToString());
    }

    /// <summary>A idade é a completa: o aniversário ainda não chegou neste ano.</summary>
    [TestMethod]
    public void CalculaIdadeCompleta()
    {
        Morador morador = Morador.Cadastrar("Lurdes A.", new DateOnly(1948, 12, 1), Cpf.Criar(DocumentosSinteticos.Cpf(9)), null, PerfilDeSaude.Vazio, Fabricas.Hoje);

        Assert.AreEqual(77, morador.Idade(Fabricas.Hoje));
        Assert.AreEqual(78, morador.Idade(new DateOnly(2026, 12, 1)));
    }

    /// <summary>Nome vazio ou nascimento no futuro são recusados, e o morador nunca aparece pelo nome.</summary>
    [TestMethod]
    public void ValidaCadastroESemDadoPessoalEmTexto()
    {
        Cpf cpf = Cpf.Criar(DocumentosSinteticos.Cpf(10));

        Assert.ThrowsExactly<RegraVioladaException>(() => Morador.Cadastrar(" ", new DateOnly(1950, 1, 1), cpf, null, PerfilDeSaude.Vazio, Fabricas.Hoje));
        Assert.ThrowsExactly<RegraVioladaException>(() => Morador.Cadastrar("Lurdes", Fabricas.Hoje.AddDays(1), cpf, null, PerfilDeSaude.Vazio, Fabricas.Hoje));
        StringAssert.DoesNotMatch(Fabricas.Morador(78).ToString(), new System.Text.RegularExpressions.Regex("Lurdes"));
    }
}
