using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Casas;

/// <summary>
/// A rotina combinada com a pessoa (D13, da entrevista): o check-in diário opcional e o lembrete de água no pico da tarde.
/// Lembretes nunca acontecem à noite (R12); esquecer o check-in diário só gera lembrete leve (R11).
/// </summary>
public sealed record Rotina
{
    /// <summary>Quanto tempo depois do horário combinado o check-in diário é considerado esquecido.</summary>
    public static readonly TimeSpan ToleranciaDoCheckInDiario = TimeSpan.FromMinutes(30);

    /// <summary>O máximo de lembretes de água por dia.</summary>
    public const int MaximoDeLembretesDeAguaPorDia = 2;

    private static readonly TimeOnly _inicioDaNoite = new(22, 0);
    private static readonly TimeOnly _fimDaNoite = new(7, 0);
    private static readonly TimeOnly _inicioDoPico = new(15, 0);
    private static readonly TimeOnly _fimDoPico = new(16, 0);

    private Rotina(TimeOnly? horarioDoCheckInDiario, bool lembreteDeAguaAtivo)
    {
        HorarioDoCheckInDiario = horarioDoCheckInDiario;
        LembreteDeAguaAtivo = lembreteDeAguaAtivo;
    }

    /// <summary>Gets a rotina inicial: sem check-in diário e com lembrete de água.</summary>
    public static Rotina Padrao { get; } = new(null, true);

    /// <summary>Gets o horário local do check-in diário, quando a pessoa quer um.</summary>
    public TimeOnly? HorarioDoCheckInDiario { get; }

    /// <summary>Gets a value indicating whether o lembrete de água está ligado.</summary>
    public bool LembreteDeAguaAtivo { get; }

    /// <summary>Cria uma rotina.</summary>
    /// <param name="horarioDoCheckInDiario">O horário local do check-in diário, ou nulo para não ter.</param>
    /// <param name="lembreteDeAguaAtivo">Se o lembrete de água fica ligado.</param>
    /// <returns>A rotina.</returns>
    /// <exception cref="RegraVioladaException">Com check-in diário marcado para a noite (R12).</exception>
    public static Rotina Criar(TimeOnly? horarioDoCheckInDiario, bool lembreteDeAguaAtivo)
    {
        if (horarioDoCheckInDiario is { } horario && EhNoite(horario))
        {
            throw new RegraVioladaException("R12", "O check-in diário não pode ser entre 22h e 7h.");
        }

        return new Rotina(horarioDoCheckInDiario, lembreteDeAguaAtivo);
    }

    /// <summary>Regra R12: entre 22h e 7h não há lembretes.</summary>
    /// <param name="horaLocal">A hora local.</param>
    /// <returns><c>true</c> à noite.</returns>
    public static bool EhNoite(TimeOnly horaLocal) => horaLocal >= _inicioDaNoite || horaLocal < _fimDaNoite;

    /// <summary>Se é hora de um lembrete de água: pico da tarde, dia quente, fora da noite e abaixo do máximo diário.</summary>
    /// <param name="horaLocal">A hora local.</param>
    /// <param name="nivelDoDia">O nível de calor do dia (leitura ou previsão).</param>
    /// <param name="lembretesJaEnviadosHoje">Quantos lembretes já foram hoje.</param>
    /// <returns><c>true</c> quando o lembrete deve ser enviado.</returns>
    public bool DeveLembrarAgua(TimeOnly horaLocal, NivelDeRisco nivelDoDia, int lembretesJaEnviadosHoje)
        => LembreteDeAguaAtivo
            && !EhNoite(horaLocal)
            && horaLocal >= _inicioDoPico && horaLocal < _fimDoPico
            && nivelDoDia >= NivelDeRisco.Atencao
            && lembretesJaEnviadosHoje < MaximoDeLembretesDeAguaPorDia;

    /// <summary>Regra R11: o check-in diário foi esquecido (o que gera só um lembrete leve, nunca um alerta).</summary>
    /// <param name="horaLocal">A hora local.</param>
    /// <param name="jaFezCheckInHoje">Se já houve um "estou bem" hoje.</param>
    /// <returns><c>true</c> quando passou da tolerância sem check-in.</returns>
    public bool CheckInDiarioEsquecido(TimeOnly horaLocal, bool jaFezCheckInHoje)
        => HorarioDoCheckInDiario is { } horario
            && !jaFezCheckInHoje
            && !EhNoite(horaLocal)
            && horaLocal >= horario.Add(ToleranciaDoCheckInDiario);
}
