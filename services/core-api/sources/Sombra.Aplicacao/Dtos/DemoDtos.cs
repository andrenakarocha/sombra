using System.ComponentModel.DataAnnotations;

namespace Sombra.Aplicacao.Dtos;

/// <summary>Quanto adiantar o relógio do Core na demonstração.</summary>
public sealed record AvancoDoRelogioDto
{
    /// <summary>Gets os minutos (até um dia).</summary>
    /// <example>15</example>
    [Required(ErrorMessage = "Informe os minutos.")]
    [Range(1, 1440, ErrorMessage = "De 1 a 1440 minutos.")]
    public int? Minutos { get; init; }
}
