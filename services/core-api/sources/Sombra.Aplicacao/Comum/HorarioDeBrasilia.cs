using System;

namespace Sombra.Aplicacao.Comum;

/// <summary>
/// O fuso do Sombra, America/Sao_Paulo: UTC−3 fixo desde o fim do horário de verão no Brasil (2019). O banco e a API
/// trabalham em UTC; o dia, a noite e o pico da tarde (R11, R12) são contados no horário local.
/// </summary>
public static class HorarioDeBrasilia
{
    /// <summary>O deslocamento em relação ao UTC.</summary>
    public static readonly TimeSpan Deslocamento = TimeSpan.FromHours(-3);

    /// <summary>O mesmo instante, no horário local.</summary>
    /// <param name="instante">O instante.</param>
    /// <returns>O instante com o deslocamento de Brasília.</returns>
    public static DateTimeOffset Local(DateTimeOffset instante) => instante.ToOffset(Deslocamento);

    /// <summary>A meia-noite local do dia do instante, em UTC.</summary>
    /// <param name="instante">O instante.</param>
    /// <returns>O começo do dia local, em UTC.</returns>
    public static DateTimeOffset InicioDoDia(DateTimeOffset instante)
        => new DateTimeOffset(Local(instante).Date, Deslocamento).ToUniversalTime();
}
