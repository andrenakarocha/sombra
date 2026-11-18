using System;
using System.Threading;
using Sombra.Aplicacao.Portas;

namespace Sombra.Infraestrutura;

/// <summary>
/// O relógio de verdade, em UTC, sobre o <see cref="TimeProvider"/> do .NET. Na demonstração ele pode ser adiantado
/// (<see cref="Avancar"/>), para o caminho B do roteiro caber num vídeo; a janela anti-replay da R10 continua no tempo
/// real, porque a Sentinela assina com o relógio dela.
/// </summary>
public sealed class RelogioDoSistema : IRelogio
{
    private readonly TimeProvider _tempo;
    private long _avanco;

    /// <summary>Initializes a new instance of the <see cref="RelogioDoSistema"/> class.</summary>
    /// <param name="tempo">A fonte de tempo.</param>
    public RelogioDoSistema(TimeProvider tempo) => _tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));

    /// <inheritdoc/>
    public DateTimeOffset Agora => _tempo.GetUtcNow().AddTicks(Interlocked.Read(ref _avanco));

    /// <summary>Adianta o relógio (só a demonstração chama).</summary>
    /// <param name="quanto">Quanto tempo.</param>
    /// <returns>O novo agora.</returns>
    public DateTimeOffset Avancar(TimeSpan quanto)
    {
        Interlocked.Add(ref _avanco, quanto.Ticks);
        return Agora;
    }
}
