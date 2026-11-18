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

/// <summary>
/// A edição de um morador, como o app manda (D17): nome, idade e fatores sempre; CPF, Cartão SUS, telefone e saúde só
/// quando a família digitou de novo (vazio quer dizer "manter").
/// </summary>
public sealed record AtualizacaoDeMoradorDto
{
    /// <summary>Gets o nome.</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(80, ErrorMessage = "O nome tem até 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Gets a idade em anos completos.</summary>
    [Required(ErrorMessage = "Informe a idade.")]
    [Range(0, 130, ErrorMessage = "A idade vai de 0 a 130 anos.")]
    public int? Idade { get; init; }

    /// <summary>Gets os fatores de risco declarados (lista vazia quando não há).</summary>
    [Required(ErrorMessage = "Informe os fatores de risco (lista vazia quando não há).")]
    public IReadOnlyList<string> FatoresDeRisco { get; init; } = [];

    /// <summary>Gets o CPF novo, quando muda.</summary>
    [StringLength(14, ErrorMessage = "O CPF tem até 14 caracteres.")]
    public string? Cpf { get; init; }

    /// <summary>Gets o Cartão SUS novo, quando muda.</summary>
    [StringLength(18, ErrorMessage = "O Cartão SUS tem até 18 caracteres.")]
    public string? CartaoSus { get; init; }

    /// <summary>Gets o telefone novo, quando muda.</summary>
    [StringLength(20, ErrorMessage = "O telefone tem até 20 caracteres.")]
    public string? Telefone { get; init; }

    /// <summary>Gets o que a família escreveu sobre a saúde, quando muda.</summary>
    [StringLength(500, ErrorMessage = "O perfil de saúde tem até 500 caracteres.")]
    public string? PerfilDeSaude { get; init; }
}

/// <summary>
/// Um morador novo, como o app manda (D17). O Core guarda a data de nascimento aproximada pela idade; o Cartão SUS é
/// obrigatório porque a leitura do app sempre espera um.
/// </summary>
public sealed record NovoMoradorDto
{
    /// <summary>Gets o nome.</summary>
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(80, ErrorMessage = "O nome tem até 80 caracteres.")]
    public string Nome { get; init; } = string.Empty;

    /// <summary>Gets a idade em anos completos.</summary>
    [Required(ErrorMessage = "Informe a idade.")]
    [Range(0, 130, ErrorMessage = "A idade vai de 0 a 130 anos.")]
    public int? Idade { get; init; }

    /// <summary>Gets o CPF (regra R1).</summary>
    [Required(ErrorMessage = "Informe o CPF.")]
    [StringLength(14, ErrorMessage = "O CPF tem até 14 caracteres.")]
    public string Cpf { get; init; } = string.Empty;

    /// <summary>Gets o Cartão SUS (regra R1).</summary>
    [Required(ErrorMessage = "Informe o Cartão SUS.")]
    [StringLength(18, ErrorMessage = "O Cartão SUS tem até 18 caracteres.")]
    public string CartaoSus { get; init; } = string.Empty;

    /// <summary>Gets o telefone da pessoa, quando houver.</summary>
    [StringLength(20, ErrorMessage = "O telefone tem até 20 caracteres.")]
    public string? Telefone { get; init; }

    /// <summary>Gets o que a família escreveu sobre a saúde (opcional; nunca sai da API).</summary>
    [StringLength(500, ErrorMessage = "O perfil de saúde tem até 500 caracteres.")]
    public string? PerfilDeSaude { get; init; }

    /// <summary>Gets os fatores de risco declarados (lista vazia quando não há).</summary>
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

/// <summary>O check-in diário, como o app manda: ligado ou não, e o horário guardado nos dois casos.</summary>
public sealed record CheckInDiarioDto
{
    /// <summary>Gets se o check-in diário fica ligado.</summary>
    [Required(ErrorMessage = "Diga se o check-in diário fica ligado.")]
    public bool? Ativo { get; init; }

    /// <summary>Gets o horário local, <c>HH:mm</c>.</summary>
    [Required(ErrorMessage = "Informe o horário do check-in diário.")]
    [RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "O horário é no formato 09:00.")]
    public string Horario { get; init; } = string.Empty;
}

/// <summary>O lembrete de água no pico da tarde (R11).</summary>
public sealed record LembreteDeAguaDto
{
    /// <summary>Gets se o lembrete fica ligado.</summary>
    [Required(ErrorMessage = "Diga se o lembrete de água fica ligado.")]
    public bool? Ativo { get; init; }
}

/// <summary>A rotina combinada com a pessoa, no formato do app (D17).</summary>
public sealed record RotinaDto
{
    /// <summary>Gets o check-in diário.</summary>
    [Required(ErrorMessage = "Informe o check-in diário.")]
    public CheckInDiarioDto? CheckInDiario { get; init; }

    /// <summary>Gets o lembrete de água.</summary>
    [Required(ErrorMessage = "Informe o lembrete de água.")]
    public LembreteDeAguaDto? LembreteDeAgua { get; init; }
}

/// <summary>A Sentinela lida no QR (ou digitada): o identificador e o código de vínculo que ela mostra.</summary>
public sealed record NovaSentinelaDto
{
    /// <summary>Gets o identificador público da Sentinela.</summary>
    [Required(ErrorMessage = "Informe o identificador da Sentinela.")]
    [RegularExpression("^snt-[0-9a-f]{6}$", ErrorMessage = "O identificador segue o formato snt-7f3a9c.")]
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>Gets o código de vínculo que a Sentinela mostra no QR.</summary>
    [Required(ErrorMessage = "Informe o código de vínculo da Sentinela.")]
    [RegularExpression("^[A-HJ-NP-Za-hj-np-z2-9]{6}$", ErrorMessage = "O código de vínculo tem 6 letras e números.")]
    public string CodigoDeVinculo { get; init; } = string.Empty;
}
