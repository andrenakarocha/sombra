using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.Autenticacao;

/// <summary>O token devolvido pelo login.</summary>
/// <param name="Token">O JWT, para o header <c>Authorization: Bearer …</c>.</param>
/// <param name="ExpiraEm">Quando o token vence.</param>
/// <param name="Papel">O papel de acesso.</param>
/// <param name="Nome">O nome de exibição.</param>
public sealed record TokenResposta(string Token, DateTimeOffset ExpiraEm, string Papel, string Nome);

/// <summary>Emite e valida os JWT do Core (HS256, chave de <c>Jwt__Segredo</c>, no mínimo 32 caracteres).</summary>
public sealed class EmissorDeToken
{
    /// <summary>A audiência dos tokens.</summary>
    public const string Audiencia = "sombra-clientes";

    private readonly IRelogio _relogio;
    private readonly TimeSpan _validade;

    /// <summary>Initializes a new instance of the <see cref="EmissorDeToken"/> class.</summary>
    /// <param name="configuracao">A configuração (<c>Jwt:Segredo</c>, <c>Jwt:Emissor</c>, <c>Jwt:HorasDeValidade</c>).</param>
    /// <param name="relogio">O relógio.</param>
    public EmissorDeToken(IConfiguration configuracao, IRelogio relogio)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        Chave = ChaveDe(configuracao);
        Emissor = configuracao["Jwt:Emissor"] ?? "sombra-core";
        _validade = TimeSpan.FromHours(double.Parse(configuracao["Jwt:HorasDeValidade"] ?? "8", CultureInfo.InvariantCulture));
    }

    /// <summary>Gets a chave de assinatura.</summary>
    public SymmetricSecurityKey Chave { get; }

    /// <summary>Gets o emissor dos tokens.</summary>
    public string Emissor { get; }

    /// <summary>Lê a chave de assinatura da configuração.</summary>
    /// <param name="configuracao">A configuração.</param>
    /// <returns>A chave.</returns>
    /// <exception cref="InvalidOperationException">Sem chave, ou com menos de 32 caracteres.</exception>
    public static SymmetricSecurityKey ChaveDe(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        string segredo = configuracao["Jwt:Segredo"] ?? string.Empty;
        return segredo.Length >= 32
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo))
            : throw new InvalidOperationException("Defina Jwt__Segredo com pelo menos 32 caracteres (make env gera um).");
    }

    /// <summary>Emite o token de uma conta.</summary>
    /// <param name="usuario">A conta autenticada.</param>
    /// <returns>O token e quando vence.</returns>
    public TokenResposta Emitir(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        DateTimeOffset expiraEm = _relogio.Agora + _validade;
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emissor,
            Audience = Audiencia,
            Expires = expiraEm.UtcDateTime,
            IssuedAt = _relogio.Agora.UtcDateTime,
            NotBefore = _relogio.Agora.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [ClaimsDoSombra.Papel] = usuario.Papel,
                [JwtRegisteredClaimNames.Name] = usuario.Nome,
            },
            SigningCredentials = new SigningCredentials(Chave, SecurityAlgorithms.HmacSha256),
        });
        return new TokenResposta(token, expiraEm, usuario.Papel, usuario.Nome);
    }
}

/// <summary>Os nomes das claims do token e a leitura do usuário logado.</summary>
public static class ClaimsDoSombra
{
    /// <summary>A claim do papel de acesso.</summary>
    public const string Papel = "role";

    /// <summary>O identificador da conta logada (claim <c>sub</c>).</summary>
    /// <param name="usuario">O usuário da requisição.</param>
    /// <returns>O identificador.</returns>
    public static Guid Id(this ClaimsPrincipal usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return Guid.Parse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Guid.Empty.ToString());
    }
}
