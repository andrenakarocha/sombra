using System;
using System.ComponentModel.DataAnnotations;

namespace Sombra.Aplicacao.Dtos;

/// <summary>Um alerta de infraestrutura do monitor da VM (spec 08).</summary>
public sealed record AlertaDeInfraDto
{
    /// <summary>Gets o servidor.</summary>
    /// <example>sombra-srv01</example>
    [Required(ErrorMessage = "Informe o host.")]
    [StringLength(64, ErrorMessage = "O host tem até 64 caracteres.")]
    public string Host { get; init; } = string.Empty;

    /// <summary>Gets o recurso: CPU, memória, disco ou serviço.</summary>
    /// <example>CPU</example>
    [Required(ErrorMessage = "Informe o recurso.")]
    [StringLength(30, ErrorMessage = "O recurso tem até 30 caracteres.")]
    public string Recurso { get; init; } = string.Empty;

    /// <summary>Gets a severidade.</summary>
    /// <example>CRITICO</example>
    [Required(ErrorMessage = "Informe a severidade.")]
    [RegularExpression("^(AVISO|CRITICO)$", ErrorMessage = "A severidade é AVISO ou CRITICO.")]
    public string Severidade { get; init; } = string.Empty;

    /// <summary>Gets o valor medido.</summary>
    /// <example>96.4</example>
    public double? Valor { get; init; }

    /// <summary>Gets o limite que foi passado.</summary>
    /// <example>90</example>
    public double? Limite { get; init; }

    /// <summary>Gets o que o monitor viu.</summary>
    /// <example>CPU acima de 90% por 3 verificações seguidas</example>
    [Required(ErrorMessage = "Informe a mensagem.")]
    [StringLength(200, ErrorMessage = "A mensagem tem até 200 caracteres.")]
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>Gets quando.</summary>
    /// <example>2026-11-18T18:05:00Z</example>
    [Required(ErrorMessage = "Informe o momento.")]
    public DateTimeOffset? Momento { get; init; }
}
