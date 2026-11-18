using System.Collections.Generic;
using System.Linq;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Sinks;

/// <summary>Um dado pessoal achado a caminho de um sink, sem o valor.</summary>
/// <param name="RegraId">A regra.</param>
/// <param name="Categoria">A família.</param>
/// <param name="Severidade">O peso.</param>
/// <param name="PeloTaint">Se foi o taint que reconheceu.</param>
public sealed record AchadoNaSaida(string RegraId, CategoriaDePii Categoria, SeveridadeDePii Severidade, bool PeloTaint);

/// <summary>O que a guarda achou num conteúdo e a versão redigida dele.</summary>
/// <param name="Achados">Os dados pessoais achados (vazio quando o conteúdo está limpo).</param>
/// <param name="Redigido">O conteúdo com cada dado trocado por <c>[PII:regra]</c>.</param>
public sealed record ResultadoDaGuarda(IReadOnlyList<AchadoNaSaida> Achados, string Redigido)
{
    /// <summary>Gets a value indicating whether o conteúdo pode sair como está.</summary>
    public bool Limpo => Achados.Count == 0;

    /// <summary>Os achados de famílias fora das permitidas.</summary>
    /// <param name="permitidas">As famílias que este sink pode deixar sair.</param>
    /// <returns>Os achados que bloqueiam a saída.</returns>
    public IReadOnlyList<AchadoNaSaida> Fora(IReadOnlyCollection<CategoriaDePii> permitidas)
        => [.. Achados.Where(a => !permitidas.Contains(a.Categoria))];
}
