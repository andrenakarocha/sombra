using System;
using System.Collections.Concurrent;
using Sombra.Aplicacao.Portas;

namespace Sombra.Infraestrutura.Clima;

/// <summary>
/// O que o Core lembra do Clima entre as chamadas (singleton): o circuito e o último risco conhecido de cada região.
/// Depois de <see cref="FalhasParaAbrir"/> falhas seguidas, o circuito abre por <see cref="TempoAberto"/> e nenhuma
/// chamada sai; enquanto isso, quem pergunta recebe o último risco conhecido, se ele tiver até
/// <see cref="ValidadeDoUltimoRisco"/>, ou nulo ("Clima indisponível").
/// </summary>
public sealed class EstadoDoClima
{
    /// <summary>Quantas falhas seguidas abrem o circuito.</summary>
    public const int FalhasParaAbrir = 3;

    /// <summary>Quanto tempo o circuito fica aberto antes de deixar uma chamada de teste passar.</summary>
    public static readonly TimeSpan TempoAberto = TimeSpan.FromSeconds(30);

    /// <summary>Até quando o último risco de uma região ainda vale como resposta.</summary>
    public static readonly TimeSpan ValidadeDoUltimoRisco = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, (RiscoDaRegiao Risco, DateTimeOffset Em)> _ultimos = new(StringComparer.Ordinal);
    private readonly TimeProvider _tempo;
    private readonly object _trava = new();
    private int _falhasSeguidas;
    private DateTimeOffset _abertoAte = DateTimeOffset.MinValue;

    /// <summary>Initializes a new instance of the <see cref="EstadoDoClima"/> class.</summary>
    /// <param name="tempo">O relógio.</param>
    public EstadoDoClima(TimeProvider tempo) => _tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));

    /// <summary>Gets a value indicating whether o circuito está aberto agora (nenhuma chamada deve sair).</summary>
    public bool Aberto
    {
        get
        {
            lock (_trava)
            {
                return _tempo.GetUtcNow() < _abertoAte;
            }
        }
    }

    /// <summary>Registra uma resposta do Clima e fecha o circuito.</summary>
    /// <returns>Se o Clima vinha falhando (para o log de <c>clima.recuperado</c>).</returns>
    public bool RegistrarSucesso()
    {
        lock (_trava)
        {
            bool vinhaFalhando = _falhasSeguidas > 0;
            _falhasSeguidas = 0;
            _abertoAte = DateTimeOffset.MinValue;
            return vinhaFalhando;
        }
    }

    /// <summary>Registra uma falha; a de número <see cref="FalhasParaAbrir"/> abre o circuito.</summary>
    /// <returns>Se esta foi a primeira falha da sequência (para o log de <c>clima.indisponivel</c>).</returns>
    public bool RegistrarFalha()
    {
        lock (_trava)
        {
            _falhasSeguidas++;
            if (_falhasSeguidas >= FalhasParaAbrir)
            {
                _abertoAte = _tempo.GetUtcNow() + TempoAberto;
            }

            return _falhasSeguidas == 1;
        }
    }

    /// <summary>Guarda o risco recém-recebido de uma região.</summary>
    /// <param name="regiao">A região.</param>
    /// <param name="risco">O risco.</param>
    public void Lembrar(string regiao, RiscoDaRegiao risco) => _ultimos[regiao] = (risco, _tempo.GetUtcNow());

    /// <summary>O último risco conhecido da região, se ainda estiver na validade.</summary>
    /// <param name="regiao">A região.</param>
    /// <returns>O risco, ou nulo.</returns>
    public RiscoDaRegiao? UltimoRisco(string regiao) =>
        _ultimos.TryGetValue(regiao, out (RiscoDaRegiao Risco, DateTimeOffset Em) ultimo) && _tempo.GetUtcNow() - ultimo.Em <= ValidadeDoUltimoRisco
            ? ultimo.Risco
            : null;
}
