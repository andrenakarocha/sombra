using System.Collections.Generic;
using System.Linq;

namespace Sombra.Dominio.Saude;

/// <summary>
/// Os fatores de risco declarados para um morador (cardiopatia, diurético). A idade avançada não é declarada: vem da data
/// de nascimento. É dado pessoal sensível (LGPD, art. 11): o <see cref="ToString"/> nunca o descreve.
/// </summary>
public sealed record PerfilDeSaude
{
    private readonly FatorDeRisco[] _fatores;

    private PerfilDeSaude(FatorDeRisco[] fatores) => _fatores = fatores;

    /// <summary>Gets um perfil sem nenhum fator declarado.</summary>
    public static PerfilDeSaude Vazio { get; } = new([]);

    /// <summary>Gets os fatores declarados, sem repetição.</summary>
    public IReadOnlyCollection<FatorDeRisco> Fatores => _fatores;

    /// <summary>Cria um perfil com os fatores declarados, ignorando repetições.</summary>
    /// <param name="fatores">Os fatores informados no cadastro.</param>
    /// <returns>O perfil.</returns>
    public static PerfilDeSaude Com(IEnumerable<FatorDeRisco> fatores)
        => new(fatores.Where(f => f is not IdadeAvancada).Distinct().ToArray());

    /// <summary>Nunca descreve a saúde da pessoa.</summary>
    /// <returns>O texto fixo <c>[DADO DE SAÚDE PROTEGIDO]</c>.</returns>
    public override string ToString() => "[DADO DE SAÚDE PROTEGIDO]";

    /// <summary>Compara pelos fatores, não pela referência da lista.</summary>
    /// <param name="other">O outro perfil.</param>
    /// <returns><c>true</c> quando os dois têm os mesmos fatores.</returns>
    public bool Equals(PerfilDeSaude? other) => other is not null && _fatores.ToHashSet().SetEquals(other._fatores);

    /// <inheritdoc/>
    public override int GetHashCode() => _fatores.Aggregate(0, (soma, f) => soma ^ f.GetHashCode());
}
