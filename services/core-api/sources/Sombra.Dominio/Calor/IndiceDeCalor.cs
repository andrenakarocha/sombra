using System;

namespace Sombra.Dominio.Calor;

/// <summary>
/// Índice de calor (temperatura aparente), calculado pela equação oficial da NWS/WPC com a leitura adotada no contrato
/// (docs/contratos/niveis-de-risco.md e ADR-006). Os 20 vetores compartilhados provam que este cálculo bate com o da
/// Sentinela, do Clima e do ML.
/// </summary>
public sealed record IndiceDeCalor
{
    private IndiceDeCalor(double celsius) => Celsius = celsius;

    /// <summary>Gets o índice em °C.</summary>
    public double Celsius { get; }

    /// <summary>Gets o nível de risco pelos limites padrão da NWS (sem ajuste pessoal).</summary>
    public NivelDeRisco NivelPadrao => LimitesTermicos.Padrao.Classificar(this);

    /// <summary>Calcula o índice de calor a partir de uma leitura.</summary>
    /// <param name="temperatura">A temperatura do ar.</param>
    /// <param name="umidade">A umidade relativa.</param>
    /// <returns>O índice de calor em °C.</returns>
    public static IndiceDeCalor Calcular(Temperatura temperatura, UmidadeRelativa umidade)
    {
        ArgumentNullException.ThrowIfNull(temperatura);
        ArgumentNullException.ThrowIfNull(umidade);

        double t = (temperatura.Celsius * 9.0 / 5.0) + 32.0;
        double ur = umidade.Percentual;
        double simples = 0.5 * (t + 61.0 + ((t - 68.0) * 1.2) + (ur * 0.094));
        double indiceF = (simples + t) / 2.0 < 80.0 ? simples : Rothfusz(t, ur);
        return new IndiceDeCalor((indiceF - 32.0) * 5.0 / 9.0);
    }

    private static double Rothfusz(double t, double ur)
    {
        double indice = -42.379 + (2.04901523 * t) + (10.14333127 * ur) - (0.22475541 * t * ur)
            - (0.00683783 * t * t) - (0.05481717 * ur * ur) + (0.00122874 * t * t * ur)
            + (0.00085282 * t * ur * ur) - (0.00000199 * t * t * ur * ur);

        if (ur < 13.0 && t >= 80.0 && t <= 112.0)
        {
            indice -= (13.0 - ur) / 4.0 * Math.Sqrt((17.0 - Math.Abs(t - 95.0)) / 17.0);
        }
        else if (ur > 85.0 && t >= 80.0 && t <= 87.0)
        {
            indice += (ur - 85.0) / 10.0 * ((87.0 - t) / 5.0);
        }

        return indice;
    }
}
