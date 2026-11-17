using System.Threading;
using System.Threading.Tasks;

namespace Sombra.Aplicacao.Portas;

/// <summary>
/// Grava numa transação só tudo o que o caso de uso mudou: agregados, leituras, check-ins e as mensagens que o
/// <see cref="INotificador"/> deixou no outbox.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Grava as mudanças.</summary>
    /// <param name="cancellationToken">Cancela a gravação.</param>
    /// <returns>A gravação.</returns>
    Task SalvarAsync(CancellationToken cancellationToken);
}
