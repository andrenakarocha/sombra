using System;
using System.ComponentModel.DataAnnotations;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;

namespace Sombra.Aplicacao.Dtos;

/// <summary>A paginação do contrato: <c>?pagina=1&amp;tamanho=20</c>, com no máximo 100 itens por página.</summary>
public record ConsultaPaginadaDto
{
    /// <summary>Gets a página, a partir de 1.</summary>
    [Range(1, 10_000, ErrorMessage = "A página vai de 1 a 10.000.")]
    public int Pagina { get; init; } = 1;

    /// <summary>Gets quantos itens por página.</summary>
    [Range(1, 100, ErrorMessage = "O tamanho da página vai de 1 a 100.")]
    public int Tamanho { get; init; } = 20;
}

/// <summary>Os filtros da lista de casas.</summary>
public sealed record FiltroDeDomiciliosDto : ConsultaPaginadaDto
{
    /// <summary>Gets o nível atual (o da última leitura).</summary>
    [EnumDataType(typeof(NivelDeRisco), ErrorMessage = "Nível desconhecido.")]
    public NivelDeRisco? Nivel { get; init; }

    /// <summary>Gets o bairro.</summary>
    [StringLength(60, ErrorMessage = "O bairro tem até 60 caracteres.")]
    public string? Bairro { get; init; }
}

/// <summary>Os filtros da lista de alertas.</summary>
public sealed record FiltroDeAlertasDto : ConsultaPaginadaDto
{
    /// <summary>Gets o estado.</summary>
    [EnumDataType(typeof(EstadoDoAlerta), ErrorMessage = "Estado desconhecido.")]
    public EstadoDoAlerta? Estado { get; init; }

    /// <summary>Gets o nível de risco.</summary>
    [EnumDataType(typeof(NivelDeRisco), ErrorMessage = "Nível desconhecido.")]
    public NivelDeRisco? Nivel { get; init; }
}

/// <summary>O período das leituras de uma casa: de (inclusive) até (exclusive).</summary>
public sealed record FiltroDeLeiturasDto : ConsultaPaginadaDto
{
    /// <summary>Gets o começo do período.</summary>
    [Required(ErrorMessage = "Informe o começo do período.")]
    public DateTimeOffset? De { get; init; }

    /// <summary>Gets o fim do período.</summary>
    [Required(ErrorMessage = "Informe o fim do período.")]
    public DateTimeOffset? Ate { get; init; }
}
