using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Sombra.Api.Autenticacao;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Api.TempoReal;

/// <summary>
/// O hub do Core (<c>docs/contratos/signalr.md</c>). Ao conectar, o Core escolhe os grupos: o Cuidador entra nos grupos
/// das casas dele; Operador e Admin, no grupo dos operadores. O cliente não chama métodos: toda ação vai pela API.
/// </summary>
[Authorize]
public sealed class HubDoSombra : Hub
{
    /// <summary>O endereço do hub.</summary>
    public const string Rota = "/hubs/sombra";

    /// <summary>O grupo de Operador e Admin.</summary>
    public const string GrupoDosOperadores = "operadores";

    private readonly ContasDeAcesso _contas;

    /// <summary>Initializes a new instance of the <see cref="HubDoSombra"/> class.</summary>
    /// <param name="contas">As contas de acesso.</param>
    public HubDoSombra(ContasDeAcesso contas) => _contas = contas ?? throw new ArgumentNullException(nameof(contas));

    /// <summary>O grupo dos Cuidadores de uma casa.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <returns>O nome do grupo.</returns>
    public static string GrupoDaCasa(Guid domicilioId) => $"domicilio:{domicilioId}";

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        if (Context.User is { } usuario)
        {
            if (usuario.IsInRole(Papeis.Cuidador))
            {
                foreach (Guid casa in await _contas.CasasDoUsuarioAsync(usuario.Id(), Context.ConnectionAborted))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, GrupoDaCasa(casa), Context.ConnectionAborted);
                }
            }
            else
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GrupoDosOperadores, Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }
}

/// <summary>O tempo real pelo hub: um evento de casa vai para o grupo dela e para os operadores; o resto, só para os operadores.</summary>
public sealed class TempoRealSignalR : ITempoReal
{
    private readonly IHubContext<HubDoSombra> _hub;

    /// <summary>Initializes a new instance of the <see cref="TempoRealSignalR"/> class.</summary>
    /// <param name="hub">O contexto do hub.</param>
    public TempoRealSignalR(IHubContext<HubDoSombra> hub) => _hub = hub ?? throw new ArgumentNullException(nameof(hub));

    /// <inheritdoc/>
    public Task PublicarAsync(string evento, Guid? domicilioId, object dados, CancellationToken cancellationToken)
    {
        IClientProxy destino = domicilioId is { } casa
            ? _hub.Clients.Groups(HubDoSombra.GrupoDosOperadores, HubDoSombra.GrupoDaCasa(casa))
            : _hub.Clients.Group(HubDoSombra.GrupoDosOperadores);
        return destino.SendAsync(evento, dados, cancellationToken);
    }
}
