using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;
using Sombra.Infraestrutura.Persistencia;

namespace Sombra.Infraestrutura.Mqtt;

/// <summary>
/// A Sentinela do seed usa o segredo do <c>.env</c> (<c>SENTINELA_SEGREDO_HEX</c>), o mesmo do simulador, para a
/// demonstração funcionar sem cadastro. Fora da demo, o segredo nasce no vínculo.
/// </summary>
public static class SentinelaDaDemo
{
    /// <summary>Acerta o segredo da Sentinela, se ela estiver vinculada.</summary>
    /// <param name="db">O contexto.</param>
    /// <param name="deviceId">A Sentinela.</param>
    /// <param name="segredoHex">O segredo.</param>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns><c>true</c> quando a Sentinela existe.</returns>
    public static async Task<bool> ConfigurarAsync(SombraDbContext db, string deviceId, string segredoHex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        Domicilio? casa = await db.Domicilios.SingleOrDefaultAsync(d => d.Sentinelas.Any(s => s.DeviceId == deviceId), cancellationToken);
        Sentinela? sentinela = casa?.Sentinelas.Single(s => s.DeviceId == deviceId);
        if (sentinela is null)
        {
            return false;
        }

        sentinela.DefinirSegredo(segredoHex);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
