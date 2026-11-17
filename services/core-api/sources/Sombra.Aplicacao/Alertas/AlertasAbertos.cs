using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// A regra R4 como o Sombra a aplica: dos alertas que pedem resposta (de Sentinela e pedido de ajuda) há no máximo um
/// aberto por casa. O preventivo só informa e convive com eles, porque a previsão da véspera ainda vale quando a casa
/// esquenta no dia seguinte.
/// </summary>
internal static class AlertasAbertos
{
    /// <summary>Busca o alerta aberto da casa que pede resposta.</summary>
    /// <param name="alertas">O repositório.</param>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O alerta, ou nulo.</returns>
    internal static async Task<AlertaDeCalor?> QuePedeRespostaAsync(IAlertaRepository alertas, Guid domicilioId, CancellationToken cancellationToken)
        => (await alertas.ListarAbertosDaCasaAsync(domicilioId, cancellationToken)).FirstOrDefault(a => a is not AlertaPreventivo);
}
