using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sombra.Aplicacao.Portas;

/// <summary>
/// Empurra eventos para o app e o painel sem polling (contrato <c>docs/contratos/signalr.md</c>). Melhor esforço: o
/// cliente que reconecta busca o estado de novo pela API.
/// </summary>
public interface ITempoReal
{
    /// <summary>Publica um evento.</summary>
    /// <param name="evento">O nome do evento do contrato (ex.: <c>AlertaAberto</c>).</param>
    /// <param name="domicilioId">A casa, para o grupo dos Cuidadores dela; nulo para só os operadores.</param>
    /// <param name="dados">O payload, sem dado pessoal.</param>
    /// <param name="cancellationToken">Cancela a publicação.</param>
    /// <returns>A publicação.</returns>
    Task PublicarAsync(string evento, Guid? domicilioId, object dados, CancellationToken cancellationToken);
}
