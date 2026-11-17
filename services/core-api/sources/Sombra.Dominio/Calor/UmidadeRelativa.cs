using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Calor;

/// <summary>Umidade relativa do ar dentro da casa, em % (regra R9: de 0 a 100).</summary>
public sealed record UmidadeRelativa
{
    private UmidadeRelativa(double percentual) => Percentual = percentual;

    /// <summary>Gets o valor em %.</summary>
    public double Percentual { get; }

    /// <summary>Cria uma umidade relativa dentro da faixa física.</summary>
    /// <param name="percentual">O valor medido, em %.</param>
    /// <returns>A umidade válida.</returns>
    /// <exception cref="RegraVioladaException">Quando o valor está fora de 0 a 100 ou não é um número.</exception>
    public static UmidadeRelativa Criar(double percentual)
    {
        if (double.IsNaN(percentual) || percentual < 0.0 || percentual > 100.0)
        {
            throw new RegraVioladaException("R9", "Umidade relativa fora da faixa (0 a 100%).");
        }

        return new UmidadeRelativa(percentual);
    }
}
