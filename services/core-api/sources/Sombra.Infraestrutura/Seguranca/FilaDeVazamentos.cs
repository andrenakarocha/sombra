using System;
using System.Threading.Channels;
using Sombra.Privacidade.Sinks;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Os vazamentos bloqueados, numa fila em memória: os guardas (inclusive o de log, de dentro do Serilog) só enfileiram,
/// sem bloquear nem logar; o <see cref="Workers.VazamentosWorker"/> registra cada um. Cheia, descarta o mais antigo.
/// </summary>
public sealed class FilaDeVazamentos : IObservadorDeVazamentos
{
    private readonly Channel<VazamentoBloqueado> _fila = Channel.CreateBounded<VazamentoBloqueado>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>Gets o lado de leitura da fila.</summary>
    public ChannelReader<VazamentoBloqueado> Leitor => _fila.Reader;

    /// <inheritdoc/>
    public void Registrar(VazamentoBloqueado evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        _fila.Writer.TryWrite(evento);
    }
}
