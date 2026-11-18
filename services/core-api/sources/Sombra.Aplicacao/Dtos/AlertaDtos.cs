using System;
using System.ComponentModel.DataAnnotations;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Dtos;

/// <summary>
/// Uma medição da Sentinela, já com a assinatura conferida (MQTT ou POST assinado). A faixa física é regra do domínio
/// (R9) e dá 422.
/// </summary>
public sealed record NovaLeituraDto
{
    /// <summary>A Sentinela que mediu.</summary>
    /// <example>snt-7f3a9c</example>
    [Required(ErrorMessage = "Informe o identificador da Sentinela.")]
    [RegularExpression("^snt-[0-9a-f]{6}$", ErrorMessage = "O identificador segue o formato snt-7f3a9c.")]
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>O instante da medição.</summary>
    /// <example>2026-11-18T18:00:00Z</example>
    [Required(ErrorMessage = "Informe o instante da medição.")]
    public DateTimeOffset? Momento { get; init; }

    /// <summary>A temperatura do ar, em °C.</summary>
    /// <example>36.0</example>
    [Required(ErrorMessage = "Informe a temperatura.")]
    public double? TemperaturaC { get; init; }

    /// <summary>A umidade relativa, em %.</summary>
    /// <example>60.0</example>
    [Required(ErrorMessage = "Informe a umidade.")]
    public double? UmidadePct { get; init; }
}

/// <summary>Um "estou bem" ou "preciso de ajuda".</summary>
public sealed record NovoCheckInDto
{
    /// <summary>De onde veio.</summary>
    /// <example>APP</example>
    [Required(ErrorMessage = "Informe a origem do check-in.")]
    [EnumDataType(typeof(OrigemDoCheckIn), ErrorMessage = "Origem desconhecida.")]
    public OrigemDoCheckIn? Origem { get; init; }

    /// <summary>O que a pessoa disse.</summary>
    /// <example>ESTOU_BEM</example>
    [Required(ErrorMessage = "Informe a situação.")]
    [EnumDataType(typeof(SituacaoDoCheckIn), ErrorMessage = "Situação desconhecida.")]
    public SituacaoDoCheckIn? Situacao { get; init; }
}

/// <summary>A resposta de um cuidador a um alerta.</summary>
public sealed record ReconhecimentoDto
{
    /// <summary>A ação: vou ligar, estou indo ou ela está bem.</summary>
    /// <example>ESTOU_INDO</example>
    [Required(ErrorMessage = "Informe a ação.")]
    [EnumDataType(typeof(AcaoDoCuidador), ErrorMessage = "Ação desconhecida.")]
    public AcaoDoCuidador? Acao { get; init; }
}
