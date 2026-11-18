using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sombra.Aplicacao.Comum;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.Autenticacao;

/// <summary>
/// Um Cuidador só mexe nas casas dele (o <c>{id}</c> da rota). Operador e Admin passam. A lista de casas vem do banco a
/// cada requisição, então uma casa recém-cadastrada vale sem novo login.
/// </summary>
public sealed class CasaDoCuidador : IAuthorizationRequirement
{
    /// <summary>O nome da política.</summary>
    public const string Politica = "CasaDoCuidador";
}

/// <summary>Confere a <see cref="CasaDoCuidador"/>.</summary>
public sealed class CasaDoCuidadorHandler : AuthorizationHandler<CasaDoCuidador, HttpContext>
{
    private readonly ContasDeAcesso _contas;

    /// <summary>Initializes a new instance of the <see cref="CasaDoCuidadorHandler"/> class.</summary>
    /// <param name="contas">As contas de acesso.</param>
    public CasaDoCuidadorHandler(ContasDeAcesso contas) => _contas = contas ?? throw new ArgumentNullException(nameof(contas));

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CasaDoCuidador requirement, HttpContext resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        if (!context.User.IsInRole(Papeis.Cuidador))
        {
            context.Succeed(requirement);
            return;
        }

        if (resource.GetRouteValue("id") is string texto && Guid.TryParse(texto, out Guid casa)
            && (await _contas.CasasDoUsuarioAsync(context.User.Id(), resource.RequestAborted)).Contains(casa))
        {
            context.Succeed(requirement);
        }
    }
}
