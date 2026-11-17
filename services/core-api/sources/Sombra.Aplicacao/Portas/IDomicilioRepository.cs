using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Portas;

/// <summary>As casas, com moradores, rede de cuidado, rotina e Sentinela (o agregado inteiro).</summary>
public interface IDomicilioRepository
{
    /// <summary>Busca uma casa.</summary>
    /// <param name="id">O identificador.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A casa, ou nulo.</returns>
    Task<Domicilio?> ObterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Busca a casa da Sentinela.</summary>
    /// <param name="deviceId">O identificador da Sentinela.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A casa, ou nulo quando a Sentinela não está vinculada.</returns>
    Task<Domicilio?> ObterPorSentinelaAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Lista as casas com monitoramento ligado.</summary>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>As casas monitoradas.</returns>
    Task<IReadOnlyList<Domicilio>> ListarMonitoradosAsync(CancellationToken cancellationToken);

    /// <summary>Inclui uma casa nova.</summary>
    /// <param name="domicilio">A casa.</param>
    void Adicionar(Domicilio domicilio);

    /// <summary>Remove uma casa.</summary>
    /// <param name="domicilio">A casa.</param>
    void Remover(Domicilio domicilio);
}
