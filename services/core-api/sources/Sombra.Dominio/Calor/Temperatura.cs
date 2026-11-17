using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Calor;

/// <summary>Temperatura do ar dentro da casa, em °C, na faixa física da Sentinela (regra R9: de −10 a 60 °C).</summary>
public sealed record Temperatura
{
    /// <summary>A menor temperatura aceita, em °C.</summary>
    public const double Minima = -10.0;

    /// <summary>A maior temperatura aceita, em °C.</summary>
    public const double Maxima = 60.0;

    private Temperatura(double celsius) => Celsius = celsius;

    /// <summary>Gets o valor em °C.</summary>
    public double Celsius { get; }

    /// <summary>Cria uma temperatura dentro da faixa física.</summary>
    /// <param name="celsius">O valor medido, em °C.</param>
    /// <returns>A temperatura válida.</returns>
    /// <exception cref="RegraVioladaException">Quando o valor está fora da faixa ou não é um número.</exception>
    public static Temperatura Criar(double celsius)
    {
        if (double.IsNaN(celsius) || celsius < Minima || celsius > Maxima)
        {
            throw new RegraVioladaException("R9", "Temperatura fora da faixa física da Sentinela (−10 a 60 °C).");
        }

        return new Temperatura(celsius);
    }
}
