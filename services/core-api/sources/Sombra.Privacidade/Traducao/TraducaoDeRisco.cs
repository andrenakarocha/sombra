namespace Sombra.Privacidade.Traducao;

/// <summary>As três traduções de um evento de segurança (spec 07 §3).</summary>
/// <param name="Cidadao">Para a pessoa de 60+: duas frases, sem jargão, tom calmo, o que aconteceu e o que fazer.</param>
/// <param name="Gestao">Para a gestão: impacto e risco LGPD em uma linha.</param>
/// <param name="Tecnico">Para o time técnico: a sugestão de correção (qual sink, qual padrão usar). É a remediação.</param>
/// <param name="Fonte"><see cref="FonteIa"/> ou <see cref="FonteTemplate"/>.</param>
public sealed record TraducaoDeRisco(string Cidadao, string Gestao, string Tecnico, string Fonte)
{
    /// <summary>Traduzida pela IA e aprovada pela guarda.</summary>
    public const string FonteIa = "IA";

    /// <summary>Texto fixo: sem chave, timeout, erro do provedor ou resposta reprovada.</summary>
    public const string FonteTemplate = "template";
}
