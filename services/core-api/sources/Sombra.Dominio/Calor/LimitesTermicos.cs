using System;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Calor;

/// <summary>
/// Os limiares de índice de calor (°C) de uma pessoa. O padrão é o da NWS; os fatores de risco do morador reduzem os
/// limiares a partir de <see cref="NivelDeRisco.CautelaExtrema"/> pela soma das reduções, sem teto (regra R3, a do app:
/// D17). O de <see cref="NivelDeRisco.Atencao"/> nunca muda, e nenhum fica abaixo dele: o piso de 26,67 °C.
/// </summary>
public sealed record LimitesTermicos
{
    private LimitesTermicos(double atencao, double cautelaExtrema, double perigo, double perigoExtremo)
    {
        Atencao = atencao;
        CautelaExtrema = cautelaExtrema;
        Perigo = perigo;
        PerigoExtremo = perigoExtremo;
    }

    /// <summary>Gets os limites da tabela da NWS (80, 90, 103 e 125 °F convertidos com duas casas).</summary>
    public static LimitesTermicos Padrao { get; } = new(26.67, 32.22, 39.44, 51.67);

    /// <summary>Gets o início de <see cref="NivelDeRisco.Atencao"/>, em °C.</summary>
    public double Atencao { get; }

    /// <summary>Gets o início de <see cref="NivelDeRisco.CautelaExtrema"/>, em °C.</summary>
    public double CautelaExtrema { get; }

    /// <summary>Gets o início de <see cref="NivelDeRisco.Perigo"/>, em °C.</summary>
    public double Perigo { get; }

    /// <summary>Gets o início de <see cref="NivelDeRisco.PerigoExtremo"/>, em °C.</summary>
    public double PerigoExtremo { get; }

    /// <summary>Devolve os limites padrão reduzidos para uma pessoa mais vulnerável ao calor (regra R3).</summary>
    /// <param name="graus">Quantos °C reduzir (a soma dos fatores, sem teto).</param>
    /// <returns>Os limites pessoais, com duas casas, como o app calcula.</returns>
    /// <exception cref="RegraVioladaException">Quando a redução é negativa ou não é um número.</exception>
    public static LimitesTermicos ComReducao(double graus)
    {
        if (double.IsNaN(graus) || graus < 0.0)
        {
            throw new RegraVioladaException("R3", "A redução dos limites térmicos não pode ser negativa.");
        }

        return new LimitesTermicos(Padrao.Atencao, Baixar(Padrao.CautelaExtrema, graus), Baixar(Padrao.Perigo, graus), Baixar(Padrao.PerigoExtremo, graus));
    }

    /// <summary>Classifica um índice de calor por estes limites (intervalo fechado embaixo e aberto em cima).</summary>
    /// <param name="indice">O índice de calor.</param>
    /// <returns>O nível de risco.</returns>
    public NivelDeRisco Classificar(IndiceDeCalor indice)
    {
        ArgumentNullException.ThrowIfNull(indice);
        return Classificar(indice.Celsius);
    }

    /// <summary>Classifica um valor de índice de calor em °C (o da previsão, ou o dos vetores da R3).</summary>
    /// <param name="c">O índice de calor, em °C.</param>
    /// <returns>O nível de risco.</returns>
    public NivelDeRisco Classificar(double c)
        => c >= PerigoExtremo ? NivelDeRisco.PerigoExtremo
            : c >= Perigo ? NivelDeRisco.Perigo
            : c >= CautelaExtrema ? NivelDeRisco.CautelaExtrema
            : c >= Atencao ? NivelDeRisco.Atencao
            : NivelDeRisco.Normal;

    private static double Baixar(double limiar, double graus)
        => Math.Round(Math.Max(Padrao.Atencao, limiar - graus), 2, MidpointRounding.AwayFromZero);
}
