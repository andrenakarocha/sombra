using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Dtos;

/// <summary>O corpo do cadastro de uma casa. O formato do endereço e da região é regra do domínio.</summary>
public sealed record NovoDomicilioDto
{
    /// <summary>Como a família chama a casa (ex.: "Casa da Dona Lurdes").</summary>
    /// <example>Casa da Dona Lurdes</example>
    [Required(ErrorMessage = "Informe o apelido da casa.")]
    [StringLength(60, ErrorMessage = "O apelido tem até 60 caracteres.")]
    public string Apelido { get; init; } = string.Empty;

    /// <summary>O logradouro.</summary>
    /// <example>Rua das Flores</example>
    [Required(ErrorMessage = "Informe o logradouro.")]
    [StringLength(120, ErrorMessage = "O logradouro tem até 120 caracteres.")]
    public string Logradouro { get; init; } = string.Empty;

    /// <summary>O número.</summary>
    /// <example>12</example>
    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(10, ErrorMessage = "O número tem até 10 caracteres.")]
    public string Numero { get; init; } = string.Empty;

    /// <summary>O bairro.</summary>
    /// <example>Santana</example>
    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(60, ErrorMessage = "O bairro tem até 60 caracteres.")]
    public string Bairro { get; init; } = string.Empty;

    /// <summary>A cidade.</summary>
    /// <example>São Paulo</example>
    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(60, ErrorMessage = "A cidade tem até 60 caracteres.")]
    public string Cidade { get; init; } = string.Empty;

    /// <summary>A UF.</summary>
    /// <example>SP</example>
    [Required(ErrorMessage = "Informe a UF.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF tem 2 letras.")]
    public string Uf { get; init; } = string.Empty;

    /// <summary>O CEP, com ou sem hífen.</summary>
    /// <example>02012-000</example>
    [Required(ErrorMessage = "Informe o CEP.")]
    [StringLength(9, ErrorMessage = "O CEP tem até 9 caracteres.")]
    public string Cep { get; init; } = string.Empty;

    /// <summary>O código da região climática (ex.: <c>SP-MIRANTE</c>).</summary>
    /// <example>SP-MIRANTE</example>
    [Required(ErrorMessage = "Informe a região climática.")]
    [StringLength(33, ErrorMessage = "A região climática tem até 33 caracteres.")]
    public string RegiaoClimatica { get; init; } = string.Empty;
}

/// <summary>A correção do cadastro da casa: apelido e endereço (a região climática não muda).</summary>
public sealed record AtualizacaoDeDomicilioDto
{
    /// <summary>Como a família chama a casa.</summary>
    /// <example>Casa da Dona Lurdes</example>
    [Required(ErrorMessage = "Informe o apelido da casa.")]
    [StringLength(60, ErrorMessage = "O apelido tem até 60 caracteres.")]
    public string Apelido { get; init; } = string.Empty;

    /// <summary>O logradouro.</summary>
    /// <example>Rua das Flores</example>
    [Required(ErrorMessage = "Informe o logradouro.")]
    [StringLength(120, ErrorMessage = "O logradouro tem até 120 caracteres.")]
    public string Logradouro { get; init; } = string.Empty;

    /// <summary>O número.</summary>
    /// <example>12</example>
    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(10, ErrorMessage = "O número tem até 10 caracteres.")]
    public string Numero { get; init; } = string.Empty;

    /// <summary>O bairro.</summary>
    /// <example>Santana</example>
    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(60, ErrorMessage = "O bairro tem até 60 caracteres.")]
    public string Bairro { get; init; } = string.Empty;

    /// <summary>A cidade.</summary>
    /// <example>São Paulo</example>
    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(60, ErrorMessage = "A cidade tem até 60 caracteres.")]
    public string Cidade { get; init; } = string.Empty;

    /// <summary>A UF.</summary>
    /// <example>SP</example>
    [Required(ErrorMessage = "Informe a UF.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF tem 2 letras.")]
    public string Uf { get; init; } = string.Empty;

    /// <summary>O CEP, com ou sem hífen.</summary>
    /// <example>02012-000</example>
    [Required(ErrorMessage = "Informe o CEP.")]
    [StringLength(9, ErrorMessage = "O CEP tem até 9 caracteres.")]
    public string Cep { get; init; } = string.Empty;
}

/// <summary>Os fatores de risco declarados de um morador (a idade avançada vem da data de nascimento).</summary>
public sealed record PerfilDoMoradorDto
{
    /// <summary>Os códigos dos fatores (lista vazia quando não há).</summary>
    /// <example>["CARDIOPATIA"]</example>
    [Required(ErrorMessage = "Informe os fatores de risco (lista vazia quando não há).")]
    public IReadOnlyList<string> FatoresDeRisco { get; init; } = [];
}

/// <summary>O corpo do cadastro de um morador. CPF e Cartão SUS são conferidos pelo dígito verificador no domínio (R1).</summary>
public sealed record NovoMoradorDto
{
    /// <summary>O nome.</summary>
    /// <example>Lurdes A.</example>
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(80, ErrorMessage = "O nome tem até 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>A data de nascimento.</summary>
    /// <example>1948-04-02</example>
    [Required(ErrorMessage = "Informe a data de nascimento.")]
    public DateOnly? DataDeNascimento { get; init; }

    /// <summary>O CPF, com ou sem pontuação.</summary>
    /// <example>348.757.636-89</example>
    [Required(ErrorMessage = "Informe o CPF.")]
    [StringLength(14, ErrorMessage = "O CPF tem até 14 caracteres.")]
    public string Cpf { get; init; } = string.Empty;

    /// <summary>O Cartão SUS, quando houver.</summary>
    /// <example>703093750001765</example>
    [StringLength(18, ErrorMessage = "O Cartão SUS tem até 18 caracteres.")]
    public string? CartaoSus { get; init; }

    /// <summary>Os códigos dos fatores de risco declarados (ex.: <c>CARDIOPATIA</c>); lista vazia quando não há.</summary>
    /// <example>["USO_DE_DIURETICO"]</example>
    [Required(ErrorMessage = "Informe os fatores de risco (lista vazia quando não há).")]
    public IReadOnlyList<string> FatoresDeRisco { get; init; } = [];
}

/// <summary>Um contato da rede de cuidado.</summary>
public sealed record ContatoDto
{
    /// <summary>O nome ou apelido do contato.</summary>
    /// <example>Ana (filha)</example>
    [Required(ErrorMessage = "Informe o nome do contato.")]
    [StringLength(60, ErrorMessage = "O nome do contato tem até 60 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>O telefone com DDD.</summary>
    /// <example>(11) 98765-4321</example>
    [Required(ErrorMessage = "Informe o telefone do contato.")]
    [StringLength(20, ErrorMessage = "O telefone tem até 20 caracteres.")]
    public string Telefone { get; init; } = string.Empty;

    /// <summary>O papel no escalonamento.</summary>
    /// <example>FAMILIAR</example>
    [Required(ErrorMessage = "Informe o papel do contato.")]
    [EnumDataType(typeof(PapelDoContato), ErrorMessage = "Papel desconhecido.")]
    public PapelDoContato? Papel { get; init; }
}

/// <summary>A rede de cuidado inteira, na ordem em que o escalonamento procura os contatos.</summary>
public sealed record RedeDeCuidadoDto
{
    /// <summary>Os contatos, do primeiro ao último.</summary>
    [Required(ErrorMessage = "Informe os contatos.")]
    public IReadOnlyList<ContatoDto> Contatos { get; init; } = [];
}

/// <summary>A rotina combinada com a pessoa (R11, R12).</summary>
public sealed record RotinaDto
{
    /// <summary>O horário local do check-in diário, ou nulo para não ter.</summary>
    /// <example>09:00:00</example>
    public TimeOnly? HorarioDoCheckInDiario { get; init; }

    /// <summary>Se o lembrete de água fica ligado.</summary>
    /// <example>true</example>
    [Required(ErrorMessage = "Diga se o lembrete de água fica ligado.")]
    public bool? LembreteDeAguaAtivo { get; init; }
}

/// <summary>O vínculo de uma Sentinela com a casa.</summary>
public sealed record NovaSentinelaDto
{
    /// <summary>O identificador público da Sentinela (ex.: <c>snt-7f3a9c</c>).</summary>
    /// <example>snt-7f3a9c</example>
    [Required(ErrorMessage = "Informe o identificador da Sentinela.")]
    [RegularExpression("^snt-[0-9a-f]{6}$", ErrorMessage = "O identificador segue o formato snt-7f3a9c.")]
    public string DeviceId { get; init; } = string.Empty;
}
