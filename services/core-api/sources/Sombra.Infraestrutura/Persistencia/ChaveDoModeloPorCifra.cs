using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// O EF guarda um modelo por tipo de contexto, e os conversores cifrados vivem no modelo. Sem isto, dois hosts no mesmo
/// processo com chaves diferentes usariam a chave do primeiro. A chave do cache inclui a impressão do par de chaves.
/// </summary>
internal sealed class ChaveDoModeloPorCifra : IModelCacheKeyFactory
{
    /// <inheritdoc/>
    public object Create(DbContext context, bool designTime)
        => (context?.GetType(), (context as SombraDbContext)?.ImpressaoDaCifra, designTime);
}
