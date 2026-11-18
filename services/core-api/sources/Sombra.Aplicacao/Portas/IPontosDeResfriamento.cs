using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Geo;

namespace Sombra.Aplicacao.Portas;

/// <summary>
/// Os lugares frescos perto de um ponto. Hoje atende uma lista de exemplo do Core; na F3.9 quem atende é o Clima
/// (<c>GET /pontos-de-resfriamento/proximos</c>), com o mesmo formato.
/// </summary>
public interface IPontosDeResfriamento
{
    /// <summary>Os pontos dentro do raio, do mais perto ao mais longe.</summary>
    /// <param name="origem">De onde medir.</param>
    /// <param name="raioKm">O raio, em km.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os pontos.</returns>
    Task<IReadOnlyList<PontoDeResfriamentoResposta>> ProximosAsync(PontoGeografico origem, double raioKm, CancellationToken cancellationToken);
}
