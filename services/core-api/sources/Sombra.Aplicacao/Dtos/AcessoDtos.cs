using System.ComponentModel.DataAnnotations;

namespace Sombra.Aplicacao.Dtos;

/// <summary>As credenciais do login.</summary>
public sealed record LoginDto
{
    /// <summary>O e-mail da conta.</summary>
    /// <example>ana.filha@demo.sombra</example>
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(120, ErrorMessage = "O e-mail tem até 120 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>A senha (nunca vai para log, resposta ou banco: o banco guarda só o hash).</summary>
    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "A senha tem de 8 a 128 caracteres.")]
    public string Senha { get; init; } = string.Empty;
}
