using System;
using System.Threading.Channels;
using Sombra.Aplicacao.Portas;
using Sombra.Privacidade.Sinks;
using Sombra.Privacidade.Taint;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Os vazamentos bloqueados, numa fila em memória: os guardas (inclusive o de log, de dentro do Serilog) só enfileiram,
/// sem bloquear nem logar; o <see cref="Workers.VazamentosWorker"/> registra cada um. Cheia, descarta o mais antigo.
/// </summary>
public sealed class FilaDeVazamentos : IObservadorDeVazamentos
{
    private readonly Channel<VazamentoBloqueado> _fila = Channel.CreateBounded<VazamentoBloqueado>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    private readonly IRelogio _relogio;

    /// <summary>Initializes a new instance of the <see cref="FilaDeVazamentos"/> class.</summary>
    /// <param name="relogio">O relógio do sistema (o da demo pode estar adiantado).</param>
    public FilaDeVazamentos(IRelogio relogio) => _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));

    /// <summary>Gets o lado de leitura da fila.</summary>
    public ChannelReader<VazamentoBloqueado> Leitor => _fila.Reader;

    /// <inheritdoc/>
    public void Registrar(VazamentoBloqueado evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        // Chamado de dentro da requisição: o momento e a casa vêm daqui, antes de o worker (fora dela) receber.
        _fila.Writer.TryWrite(evento with
        {
            Momento = evento.Momento ?? _relogio.Agora,
            DomicilioId = evento.DomicilioId ?? ContextoDeTaint.Atual?.DomicilioId,
        });
    }
}
