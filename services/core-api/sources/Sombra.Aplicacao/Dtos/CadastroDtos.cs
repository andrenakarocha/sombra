using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Dtos;

/// <summary>O corpo do cadastro de uma casa. O formato do endereço e da região é regra do domínio.</summary>
public sealed record NovoDomicilioDto
{
    /// <summary>Gets como a família chama a casa (ex.: "Casa da Dona Lurdes").</summary>
    [Required(ErrorMessage = "Informe o apelido da casa.")]
    [StringLength(60, ErrorMessage = "O apelido tem até 60 caracteres.")]
    public string Apelido { get; init; } = string.Empty;

    /// <summary>Gets o logradouro.</summary>
    [Required(ErrorMessage = "Informe o logradouro.")]
    [StringLength(120, ErrorMessage = "O logradouro tem até 120 caracteres.")]
    public string Logradouro { get; init; } = string.Empty;

    /// <summary>Gets o número.</summary>
    [Required(ErrorMessage = "Informe o número.")]
    [StringLength(10, ErrorMessage = "O número tem até 10 caracteres.")]
    public string Numero { get; init; } = string.Empty;

    /// <summary>Gets o bairro.</summary>
    [Required(ErrorMessage = "Informe o bairro.")]
    [StringLength(60, ErrorMessage = "O bairro tem até 60 caracteres.")]
    public string Bairro { get; init; } = string.Empty;

    /// <summary>Gets a cidade.</summary>
    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(60, ErrorMessage = "A cidade tem até 60 caracteres.")]
    public string Cidade { get; init; } = string.Empty;

    /// <summary>Gets a UF.</summary>
    [Required(ErrorMessage = "Informe a UF.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF tem 2 letras.")]
    public string Uf { get; init; } = string.Empty;

    /// <summary>Gets o CEP, com ou sem hífen.</summary>
    [Required(ErrorMessage = "Informe o CEP.")]
    [StringLength(9, ErrorMessage = "O CEP tem até 9 caracteres.")]
    public string Cep { get; init; } = string.Empty;

    /// <summary>Gets o código da região climática (ex.: <c>SP-MIRANTE</c>).</summary>
    [Required(ErrorMessage = "Informe a região climática.")]
    [StringLength(33, ErrorMessage = "A região climática tem até 33 caracteres.")]
    public string RegiaoClimatica { get; init; } = string.Empty;
}

/// <summary>O corpo do cadastro de um morador. CPF e Cartão SUS são conferidos pelo dígito verificador no domínio (R1).</summary>
public sealed record NovoMoradorDto
{
    /// <summary>Gets o nome.</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(80, ErrorMessage = "O nome tem até 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Gets a data de nascimento.</summary>
    [Required(ErrorMessage = "Informe a data de nascimento.")]
    public DateOnly? DataDeNascimento { get; init; }

    /// <summary>Gets o CPF, com ou sem pontuação.</summary>
    [Required(ErrorMessage = "Informe o CPF.")]
    [StringLength(14, ErrorMessage = "O CPF tem até 14 caracteres.")]
    public string Cpf { get; init; } = string.Empty;

    /// <summary>Gets o Cartão SUS, quando houver.</summary>
    [StringLength(18, ErrorMessage = "O Cartão SUS tem até 18 caracteres.")]
    public string? CartaoSus { get; init; }

    /// <summary>Gets os códigos dos fatores de risco declarados (ex.: <c>CARDIOPATIA</c>); lista vazia quando não há.</summary>
    [Required(ErrorMessage = "Informe os fatores de risco (lista vazia quando não há).")]
    public IReadOnlyList<string> FatoresDeRisco { get; init; } = [];
}

/// <summary>Um contato da rede de cuidado.</summary>
public sealed record ContatoDto
{
    /// <summary>Gets o nome ou apelido do contato.</summary>
    [Required(ErrorMessage = "Informe o nome do contato.")]
    [StringLength(60, ErrorMessage = "O nome do contato tem até 60 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Gets o telefone com DDD.</summary>
    [Required(ErrorMessage = "Informe o telefone do contato.")]
    [StringLength(20, ErrorMessage = "O telefone tem até 20 caracteres.")]
    public string Telefone { get; init; } = string.Empty;

    /// <summary>Gets o papel no escalonamento.</summary>
    [Required(ErrorMessage = "Informe o papel do contato.")]
    [EnumDataType(typeof(PapelDoContato), ErrorMessage = "Papel desconhecido.")]
    public PapelDoContato? Papel { get; init; }
}

/// <summary>A rede de cuidado inteira, na ordem em que o escalonamento procura os contatos.</summary>
public sealed record RedeDeCuidadoDto
{
    /// <summary>Gets os contatos, do primeiro ao último.</summary>
    [Required(ErrorMessage = "Informe os contatos.")]
    public IReadOnlyList<ContatoDto> Contatos { get; init; } = [];
}

/// <summary>A rotina combinada com a pessoa (R11, R12).</summary>
public sealed record RotinaDto
{
    /// <summary>Gets o horário local do check-in diário, ou nulo para não ter.</summary>
    public TimeOnly? HorarioDoCheckInDiario { get; init; }

    /// <summary>Gets a value indicating whether o lembrete de água fica ligado.</summary>
    [Required(ErrorMessage = "Diga se o lembrete de água fica ligado.")]
    public bool? LembreteDeAguaAtivo { get; init; }
}

/// <summary>O vínculo de uma Sentinela com a casa.</summary>
public sealed record NovaSentinelaDto
{
    /// <summary>Gets o identificador público da Sentinela (ex.: <c>snt-7f3a9c</c>).</summary>
    [Required(ErrorMessage = "Informe o identificador da Sentinela.")]
    [RegularExpression("^snt-[0-9a-f]{6}$", ErrorMessage = "O identificador segue o formato snt-7f3a9c.")]
    public string DeviceId { get; init; } = string.Empty;
}
