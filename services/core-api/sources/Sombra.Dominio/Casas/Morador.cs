using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Pessoas;
using Sombra.Dominio.Saude;

namespace Sombra.Dominio.Casas;

/// <summary>A pessoa idosa que mora no domicílio. Nome, documentos e saúde são dados pessoais.</summary>
public sealed class Morador
{
    private Morador(Guid id, string nome, DateOnly dataDeNascimento, Cpf cpf, CartaoSus? cartaoSus, PerfilDeSaude perfilDeSaude)
    {
        Id = id;
        Nome = nome;
        DataDeNascimento = dataDeNascimento;
        Cpf = cpf;
        CartaoSus = cartaoSus;
        PerfilDeSaude = perfilDeSaude;
    }

    /// <summary>Gets o identificador do morador.</summary>
    public Guid Id { get; }

    /// <summary>Gets o nome.</summary>
    public string Nome { get; }

    /// <summary>Gets a data de nascimento.</summary>
    public DateOnly DataDeNascimento { get; }

    /// <summary>Gets o CPF (regra R1).</summary>
    public Cpf Cpf { get; }

    /// <summary>Gets o Cartão SUS, quando informado (regra R1).</summary>
    public CartaoSus? CartaoSus { get; }

    /// <summary>Gets os fatores de risco declarados.</summary>
    public PerfilDeSaude PerfilDeSaude { get; private set; }

    /// <summary>Gets o telefone da pessoa, para a família ligar, quando houver.</summary>
    public Telefone? Telefone { get; private set; }

    /// <summary>Cadastra um morador.</summary>
    /// <param name="nome">O nome.</param>
    /// <param name="dataDeNascimento">A data de nascimento.</param>
    /// <param name="cpf">O CPF.</param>
    /// <param name="cartaoSus">O Cartão SUS, se houver.</param>
    /// <param name="perfil">Os fatores de risco declarados.</param>
    /// <param name="hoje">A data de hoje, para conferir o nascimento.</param>
    /// <returns>O morador.</returns>
    /// <exception cref="RegraVioladaException">Quando falta o nome ou o nascimento está no futuro.</exception>
    public static Morador Cadastrar(string nome, DateOnly dataDeNascimento, Cpf cpf, CartaoSus? cartaoSus, PerfilDeSaude perfil, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(perfil);
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new RegraVioladaException("CADASTRO", "O nome do morador é obrigatório.");
        }

        if (dataDeNascimento > hoje)
        {
            throw new RegraVioladaException("CADASTRO", "A data de nascimento não pode estar no futuro.");
        }

        return new Morador(Guid.CreateVersion7(), nome.Trim(), dataDeNascimento, cpf, cartaoSus, perfil);
    }

    /// <summary>Calcula a idade completa numa data.</summary>
    /// <param name="hoje">A data de referência.</param>
    /// <returns>A idade em anos.</returns>
    public int Idade(DateOnly hoje)
    {
        int idade = hoje.Year - DataDeNascimento.Year;
        return DataDeNascimento.AddYears(idade) > hoje ? idade - 1 : idade;
    }

    /// <summary>Os fatores de risco que valem hoje: os declarados mais a idade avançada, quando for o caso.</summary>
    /// <param name="hoje">A data de referência.</param>
    /// <returns>Os fatores.</returns>
    public IReadOnlyCollection<FatorDeRisco> FatoresDeRisco(DateOnly hoje)
        => Idade(hoje) >= IdadeAvancada.IdadeMinima ? [.. PerfilDeSaude.Fatores, new IdadeAvancada()] : [.. PerfilDeSaude.Fatores];

    /// <summary>Os limites térmicos pessoais (regra R3): o padrão reduzido pelos fatores, até o máximo.</summary>
    /// <param name="hoje">A data de referência.</param>
    /// <returns>Os limites da pessoa.</returns>
    public LimitesTermicos LimitesTermicos(DateOnly hoje)
        => Calor.LimitesTermicos.ComReducao(Math.Min(FatoresDeRisco(hoje).Sum(f => f.ReducaoGraus), Calor.LimitesTermicos.ReducaoMaxima));

    /// <summary>Atualiza os fatores de risco declarados.</summary>
    /// <param name="perfil">O novo perfil.</param>
    public void AtualizarPerfil(PerfilDeSaude perfil) => PerfilDeSaude = perfil ?? throw new ArgumentNullException(nameof(perfil));

    /// <summary>Troca o telefone da pessoa; nulo apaga.</summary>
    /// <param name="telefone">O novo telefone.</param>
    public void DefinirTelefone(Telefone? telefone) => Telefone = telefone;

    /// <summary>Nunca revela quem é a pessoa.</summary>
    /// <returns>O identificador, sem dado pessoal.</returns>
    public override string ToString() => $"Morador {Id}";
}
