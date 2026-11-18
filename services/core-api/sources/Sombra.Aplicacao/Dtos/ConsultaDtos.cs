using System;
using System.ComponentModel.DataAnnotations;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;

namespace Sombra.Aplicacao.Dtos;

/// <summary>A paginação do contrato: <c>?pagina=1&amp;tamanho=20</c>, com no máximo 100 itens por página.</summary>
public record ConsultaPaginadaDto
{
    /// <summary>A página, a partir de 1.</summary>
    /// <example>1</example>
    [Range(1, 10_000, ErrorMessage = "A página vai de 1 a 10.000.")]
    public int Pagina { get; init; } = 1;

    /// <summary>Quantos itens por página.</summary>
    /// <example>20</example>
    [Range(1, 100, ErrorMessage = "O tamanho da página vai de 1 a 100.")]
    public int Tamanho { get; init; } = 20;
}

/// <summary>Os filtros da lista de casas.</summary>
public sealed record FiltroDeDomiciliosDto : ConsultaPaginadaDto
{
    /// <summary>O nível atual (o da última leitura).</summary>
    /// <example>PERIGO</example>
    [EnumDataType(typeof(NivelDeRisco), ErrorMessage = "Nível desconhecido.")]
    public NivelDeRisco? Nivel { get; init; }

    /// <summary>O bairro.</summary>
    /// <example>Santana</example>
    [StringLength(60, ErrorMessage = "O bairro tem até 60 caracteres.")]
    public string? Bairro { get; init; }
}

/// <summary>Os filtros da lista de alertas.</summary>
public sealed record FiltroDeAlertasDto : ConsultaPaginadaDto
{
    /// <summary>O estado.</summary>
    /// <example>ESCALONADO</example>
    [EnumDataType(typeof(EstadoDoAlerta), ErrorMessage = "Estado desconhecido.")]
    public EstadoDoAlerta? Estado { get; init; }

    /// <summary>O nível de risco.</summary>
    /// <example>PERIGO</example>
    [EnumDataType(typeof(NivelDeRisco), ErrorMessage = "Nível desconhecido.")]
    public NivelDeRisco? Nivel { get; init; }
}

/// <summary>O período das leituras de uma casa: de (inclusive) até (exclusive).</summary>
public sealed record FiltroDeLeiturasDto : ConsultaPaginadaDto
{
    /// <summary>O começo do período.</summary>
    /// <example>2026-11-18T00:00:00Z</example>
    [Required(ErrorMessage = "Informe o começo do período.")]
    public DateTimeOffset? De { get; init; }

    /// <summary>O fim do período.</summary>
    /// <example>2026-11-19T00:00:00Z</example>
    [Required(ErrorMessage = "Informe o fim do período.")]
    public DateTimeOffset? Ate { get; init; }
}
