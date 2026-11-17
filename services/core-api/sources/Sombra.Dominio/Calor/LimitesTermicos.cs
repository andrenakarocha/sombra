using System;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Calor;

/// <summary>
/// Os limiares de índice de calor (°C) de uma pessoa. O padrão é o da NWS; os fatores de risco do morador reduzem os
/// limiares a partir de <see cref="NivelDeRisco.CautelaExtrema"/> em até <see cref="ReducaoMaxima"/> °C (regra R3), e o de
/// <see cref="NivelDeRisco.Atencao"/> nunca muda, o que mantém o piso de 26,67 °C.
/// </summary>
public sealed record LimitesTermicos
{
    /// <summary>A maior redução pessoal aceita, em °C.</summary>
    public const double ReducaoMaxima = 5.0;

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
    /// <param name="graus">Quantos °C reduzir, de 0 a <see cref="ReducaoMaxima"/>.</param>
    /// <returns>Os limites pessoais.</returns>
    /// <exception cref="RegraVioladaException">Quando a redução é negativa ou maior que o máximo.</exception>
    public static LimitesTermicos ComReducao(double graus)
    {
        if (double.IsNaN(graus) || graus < 0.0 || graus > ReducaoMaxima)
        {
            throw new RegraVioladaException("R3", "A redução dos limites térmicos vai de 0 a 5 °C.");
        }

        return new LimitesTermicos(Padrao.Atencao, Padrao.CautelaExtrema - graus, Padrao.Perigo - graus, Padrao.PerigoExtremo - graus);
    }

    /// <summary>Classifica um índice de calor por estes limites (intervalo fechado embaixo e aberto em cima).</summary>
    /// <param name="indice">O índice de calor.</param>
    /// <returns>O nível de risco.</returns>
    public NivelDeRisco Classificar(IndiceDeCalor indice)
    {
        ArgumentNullException.ThrowIfNull(indice);
        double c = indice.Celsius;
        return c >= PerigoExtremo ? NivelDeRisco.PerigoExtremo
            : c >= Perigo ? NivelDeRisco.Perigo
            : c >= CautelaExtrema ? NivelDeRisco.CautelaExtrema
            : c >= Atencao ? NivelDeRisco.Atencao
            : NivelDeRisco.Normal;
    }
}
