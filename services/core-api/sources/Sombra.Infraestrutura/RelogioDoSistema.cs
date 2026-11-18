using System;
using Sombra.Aplicacao.Portas;

namespace Sombra.Infraestrutura;

/// <summary>O relógio de verdade, em UTC, sobre o <see cref="TimeProvider"/> do .NET.</summary>
public sealed class RelogioDoSistema : IRelogio
{
    private readonly TimeProvider _tempo;

    /// <summary>Initializes a new instance of the <see cref="RelogioDoSistema"/> class.</summary>
    /// <param name="tempo">A fonte de tempo.</param>
    public RelogioDoSistema(TimeProvider tempo) => _tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));

    /// <inheritdoc/>
    public DateTimeOffset Agora => _tempo.GetUtcNow();
}
