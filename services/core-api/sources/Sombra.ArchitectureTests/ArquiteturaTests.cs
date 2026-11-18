using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetArchTest.Rules;
using Sombra.Aplicacao.Cadastro;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Privacidade;

namespace Sombra.ArchitectureTests;

/// <summary>
/// As camadas do Core (spec 01): o domínio não depende de nada, a aplicação só do domínio (e do módulo de privacidade),
/// a infraestrutura nunca da API, e os controllers não falam com o banco direto. Cada regra quebrada lista os tipos.
/// </summary>
[TestClass]
public sealed class ArquiteturaTests
{
    private static readonly Assembly _dominio = typeof(Domicilio).Assembly;
    private static readonly Assembly _aplicacao = typeof(CadastroDeDomicilios).Assembly;
    private static readonly Assembly _infraestrutura = typeof(SombraDbContext).Assembly;
    private static readonly Assembly _privacidade = typeof(MascaradorDePii).Assembly;
    private static readonly Assembly _api = typeof(Program).Assembly;

    private static readonly string[] _frameworksDeEntrega = ["Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "MQTTnet", "Serilog"];

    /// <summary>O domínio é C# puro: nenhuma outra camada e nenhum framework.</summary>
    [TestMethod]
    public void DominioNaoDependeDeNada()
        => Conferir(Types.InAssembly(_dominio).ShouldNot().HaveDependencyOnAny(
            ["Sombra.Aplicacao", "Sombra.Infraestrutura", "Sombra.Api", "Sombra.Privacidade", "System.Text.Json", .. _frameworksDeEntrega]).GetResult());

    /// <summary>A aplicação conhece o domínio e as portas que ela mesma define, nunca a infraestrutura, a API ou um framework.</summary>
    [TestMethod]
    public void AplicacaoNaoConheceInfraestruturaNemApi()
        => Conferir(Types.InAssembly(_aplicacao).ShouldNot().HaveDependencyOnAny(
            ["Sombra.Infraestrutura", "Sombra.Api", "System.Net.Http", .. _frameworksDeEntrega]).GetResult());

    /// <summary>A infraestrutura implementa as portas, mas não sabe que existe uma API HTTP.</summary>
    [TestMethod]
    public void InfraestruturaNaoConheceAApi()
        => Conferir(Types.InAssembly(_infraestrutura).ShouldNot().HaveDependencyOnAny("Sombra.Api", "Microsoft.AspNetCore").GetResult());

    /// <summary>O módulo de privacidade é independente: serve a qualquer camada sem depender de nenhuma.</summary>
    [TestMethod]
    public void PrivacidadeEhIndependente()
        => Conferir(Types.InAssembly(_privacidade).ShouldNot().HaveDependencyOnAny(
            ["Sombra.Dominio", "Sombra.Aplicacao", "Sombra.Infraestrutura", "Sombra.Api", .. _frameworksDeEntrega]).GetResult());

    /// <summary>Os controllers são finos: selados, com o sufixo, e sem tocar no banco (falam com casos de uso e consultas).</summary>
    [TestMethod]
    public void ControllersSaoFinos()
    {
        Assert.IsGreaterThanOrEqualTo(9, Types.InAssembly(_api).That().Inherit(typeof(ControllerBase)).GetTypes().Count());
        Conferir(Types.InAssembly(_api).That().Inherit(typeof(ControllerBase))
            .Should().BeSealed().And().HaveNameEndingWith("Controller", System.StringComparison.Ordinal)
            .And().NotHaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Sombra.Infraestrutura.Persistencia")
            .GetResult());
    }

    /// <summary>No domínio, o que não é abstrato é selado (herança só onde ela é desenho: alertas e fatores de risco).</summary>
    [TestMethod]
    public void DominioEhSelado()
    {
        Assert.IsGreaterThanOrEqualTo(20, Types.InAssembly(_dominio).That().ResideInNamespaceStartingWith("Sombra.Dominio").And().AreClasses().And().AreNotAbstract().GetTypes().Count());
        Conferir(Types.InAssembly(_dominio).That().ResideInNamespaceStartingWith("Sombra.Dominio").And().AreClasses().And().AreNotAbstract()
            .Should().BeSealed().GetResult());
    }

    /// <summary>Os eventos de domínio são registros selados, e só o domínio os cria.</summary>
    [TestMethod]
    public void EventosDeDominioSaoSelados()
        => Conferir(Types.InAssembly(_dominio).That().Inherit(typeof(EventoDeDominio)).Should().BeSealed().GetResult());

    /// <summary>As portas da aplicação são interfaces com o prefixo <c>I</c>, e os repositórios moram na infraestrutura.</summary>
    [TestMethod]
    public void PortasERepositoriosSeguemOPadrao()
    {
        Conferir(Types.InAssembly(_aplicacao).That().ResideInNamespace("Sombra.Aplicacao.Portas").And().AreInterfaces()
            .Should().HaveNameStartingWith("I", System.StringComparison.Ordinal).GetResult());
        Conferir(Types.InAssembly(_infraestrutura).That().HaveNameEndingWith("Repository", System.StringComparison.Ordinal)
            .Should().ResideInNamespace("Sombra.Infraestrutura.Persistencia.Repositorios").And().BeSealed().GetResult());
    }

    private static void Conferir(NetArchTest.Rules.TestResult resultado)
        => Assert.IsTrue(resultado.IsSuccessful, "Quebram a regra: " + string.Join(", ", resultado.FailingTypeNames ?? []));
}
