using System;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Portas;

/// <summary>As leituras da Sentinela. Ficam fora do agregado da casa porque chegam a cada 10 segundos.</summary>
public interface ILeituraRepository
{
    /// <summary>Inclui uma leitura.</summary>
    /// <param name="domicilioId">A casa da Sentinela que mediu.</param>
    /// <param name="leitura">A leitura.</param>
    void Adicionar(Guid domicilioId, Leitura leitura);

    /// <summary>Busca a leitura mais recente de uma casa.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A leitura, ou nulo quando a casa ainda não tem nenhuma.</returns>
    Task<Leitura?> ObterUltimaAsync(Guid domicilioId, CancellationToken cancellationToken);
}
