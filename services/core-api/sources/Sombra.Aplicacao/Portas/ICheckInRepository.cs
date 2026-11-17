using System;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Portas;

/// <summary>Os check-ins ("estou bem" e "preciso de ajuda"), um histórico que só cresce.</summary>
public interface ICheckInRepository
{
    /// <summary>Inclui um check-in.</summary>
    /// <param name="checkIn">O check-in.</param>
    void Adicionar(CheckIn checkIn);

    /// <summary>Busca o check-in mais recente de uma casa.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O check-in, ou nulo quando a casa ainda não tem nenhum.</returns>
    Task<CheckIn?> ObterUltimoAsync(Guid domicilioId, CancellationToken cancellationToken);
}
