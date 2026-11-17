using System.Linq;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Saude;

/// <summary>
/// O que deixa uma pessoa mais vulnerável ao calor. Cada fator reduz os limites térmicos dela (regra R3); a soma é
/// limitada em <see cref="Calor.LimitesTermicos.ReducaoMaxima"/>. Os valores são uma calibração do protótipo, não uma
/// recomendação médica.
/// </summary>
public abstract record FatorDeRisco
{
    private static readonly FatorDeRisco[] _declaraveis = [new Cardiopatia(), new UsoDeDiuretico()];

    /// <summary>Gets o código estável do fator, usado na API e no banco.</summary>
    public abstract string Codigo { get; }

    /// <summary>Gets quantos °C este fator reduz os limites térmicos.</summary>
    public abstract double ReducaoGraus { get; }

    /// <summary>Encontra um fator declarável pelo código. A idade avançada não entra: vem da data de nascimento.</summary>
    /// <param name="codigo">O código, ex.: <c>CARDIOPATIA</c>.</param>
    /// <returns>O fator.</returns>
    /// <exception cref="RegraVioladaException">Quando o código não é de um fator declarável (R3).</exception>
    public static FatorDeRisco DoCodigo(string? codigo)
    {
        string normalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        return _declaraveis.SingleOrDefault(f => f.Codigo == normalizado)
            ?? throw new RegraVioladaException("R3", "Fator de risco desconhecido.");
    }
}

/// <summary>Pessoa com 80 anos ou mais: sente menos sede e regula pior a temperatura.</summary>
public sealed record IdadeAvancada : FatorDeRisco
{
    /// <summary>A idade a partir da qual o fator vale.</summary>
    public const int IdadeMinima = 80;

    /// <inheritdoc/>
    public override string Codigo => "IDADE_AVANCADA";

    /// <inheritdoc/>
    public override double ReducaoGraus => 2.0;
}

/// <summary>Doença cardíaca: o calor sobrecarrega o coração.</summary>
public sealed record Cardiopatia : FatorDeRisco
{
    /// <inheritdoc/>
    public override string Codigo => "CARDIOPATIA";

    /// <inheritdoc/>
    public override double ReducaoGraus => 2.0;
}

/// <summary>Uso de diurético: aumenta a perda de água e o risco de desidratação.</summary>
public sealed record UsoDeDiuretico : FatorDeRisco
{
    /// <inheritdoc/>
    public override string Codigo => "USO_DE_DIURETICO";

    /// <inheritdoc/>
    public override double ReducaoGraus => 1.5;
}
