using System;
using System.Linq;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Sinks;

namespace Sombra.Privacidade.Traducao;

/// <summary>
/// A guarda na saída da IA (spec 07 §3): a tradução passa pelos mesmos detectores de PII, por uma lista de jargões
/// proibidos para o cidadão e por limites de tamanho. Reprovou, o tradutor usa o template.
/// </summary>
public static partial class GuardaDaTraducao
{
    /// <summary>O máximo da mensagem do cidadão.</summary>
    public const int MaximoCidadao = 280;

    /// <summary>O máximo da linha da gestão.</summary>
    public const int MaximoGestao = 240;

    /// <summary>O máximo da sugestão técnica.</summary>
    public const int MaximoTecnico = 500;

    private static readonly GuardaDeSaida _guarda = new(MotorDePii.Padrao);

    private static readonly string[] _jargoes =
    [
        "sink", "taint", "pii", "payload", "endpoint", "http", "mqtt", "regex", "middleware", "log", "logs", "token", "hash",
        "hmac", "lgpd", "api", "json", "stack", "sensivel", "mascaradordepii", "permitepii",
    ];

    /// <summary>Confere uma tradução da IA.</summary>
    /// <param name="traducao">A tradução.</param>
    /// <param name="motivo">Por que reprovou, sem o texto (vai para o log).</param>
    /// <returns><c>true</c> quando pode ser usada.</returns>
    public static bool Aprovar(TraducaoDeRisco traducao, out string motivo)
    {
        ArgumentNullException.ThrowIfNull(traducao);
        motivo = (traducao.Cidadao, traducao.Gestao, traducao.Tecnico) switch
        {
            var (c, g, t) when string.IsNullOrWhiteSpace(c) || string.IsNullOrWhiteSpace(g) || string.IsNullOrWhiteSpace(t) => "campo_vazio",
            var (c, g, t) when c.Length > MaximoCidadao || g.Length > MaximoGestao || t.Length > MaximoTecnico => "longa_demais",
            var (c, g, t) when !_guarda.Verificar(c, null).Limpo || !_guarda.Verificar(g, null).Limpo || !_guarda.Verificar(t, null).Limpo => "dado_pessoal",
            var (c, _, _) when FimDeFrase().Count(c.Trim()) > 2 => "cidadao_com_mais_de_duas_frases",
            var (c, _, _) when Palavra().Matches(c.ToLowerInvariant()).Any(p => _jargoes.Contains(p.Value)) => "jargao_para_o_cidadao",
            _ => string.Empty,
        };
        return motivo.Length == 0;
    }

    // Uma frase termina em pontuação seguida de espaço ou do fim: "art. 11" no meio não conta, nem os pontos de um número.
    [GeneratedRegex(@"[.!?]+(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex FimDeFrase();

    [GeneratedRegex(@"\p{L}+", RegexOptions.CultureInvariant)]
    private static partial Regex Palavra();
}
