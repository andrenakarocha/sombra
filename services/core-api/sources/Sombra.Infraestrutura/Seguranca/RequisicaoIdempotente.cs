using System;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Uma escrita feita com <c>Idempotency-Key</c>: quem fez, a rota e a resposta, para a repetição devolver o mesmo sem
/// repetir o efeito. Sem dado pessoal: o corpo é o de check-in e de alerta, só com IDs, níveis e textos de sistema.
/// </summary>
public sealed class RequisicaoIdempotente
{
    /// <summary>Gets or sets quem fez a requisição.</summary>
    public Guid Usuario { get; set; }

    /// <summary>Gets or sets a <c>Idempotency-Key</c>.</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Gets or sets o método e a rota.</summary>
    public string Rota { get; set; } = string.Empty;

    /// <summary>Gets or sets o status HTTP; nulo enquanto a primeira requisição roda.</summary>
    public int? Status { get; set; }

    /// <summary>Gets or sets o corpo JSON da resposta.</summary>
    public string? Corpo { get; set; }

    /// <summary>Gets or sets quando a chave foi usada pela primeira vez.</summary>
    public DateTimeOffset CriadaEm { get; set; }
}
