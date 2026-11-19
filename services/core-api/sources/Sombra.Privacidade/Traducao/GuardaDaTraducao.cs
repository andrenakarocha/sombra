using System;
using System.Linq;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Sinks;

namespace Sombra.Privacidade.Traducao;

/// <summary>
/// A guarda na saída da IA (spec 07 §3): a tradução passa pelos mesmos detectores de PII, por uma lista de jargões
/// proibidos para o cidadão, por limites de tamanho e pela ortografia do texto do cidadão e da gestão (palavra que exige
/// acento escrita sem ele). Reprovou, o tradutor usa o template.
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

    // Palavras que só existem com acento: escritas sem ele, a IA ignorou a ortografia que o prompt pede. Nenhuma é outra
    // palavra válida sem o acento. Vale para o texto do cidadão e o da gestão, que são prosa; a sugestão técnica cita
    // identificadores (a regra "saude", Privacidade__Aplicacao) e fica de fora.
    private static readonly string[] _semAcento =
    [
        "nao", "voce", "voces", "estao", "entao", "sao", "ninguem", "tambem", "ate", "possivel", "saude", "seguranca",
        "informacao", "informacoes", "protecao", "autorizacao", "identificacao", "familia", "codigo", "servico", "conexao",
        "numero", "endereco", "usuario", "horario", "relogio", "exposicao", "violacao", "correcao", "validacao",
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
            var (c, g, _) when SemAcento(c) || SemAcento(g) => "sem_acento",
            _ => string.Empty,
        };
        return motivo.Length == 0;
    }

    /// <summary>
    /// Confere a tradução e a coerência com o evento: a forma não basta, porque a IA já respondeu com o sentido trocado
    /// (casos reais, nos testes). A correção técnica precisa citar o remédio do tipo de evento, o cidadão não pode ler que
    /// o Sombra tentou enviar o dado num acesso negado, e ninguém promete o que o sistema não faz.
    /// </summary>
    /// <param name="traducao">A tradução.</param>
    /// <param name="evento">O evento traduzido.</param>
    /// <param name="motivo">Por que reprovou, sem o texto (vai para o log).</param>
    /// <returns><c>true</c> quando pode ser usada.</returns>
    public static bool Aprovar(TraducaoDeRisco traducao, EventoParaTraduzir evento, out string motivo)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (!Aprovar(traducao, out motivo))
        {
            return false;
        }

        string cidadao = traducao.Cidadao.ToLowerInvariant();
        string tecnico = traducao.Tecnico.ToLowerInvariant();
        motivo = evento switch
        {
            _ when Palavra().Matches(cidadao).Any(p => p.Value is "equipe") => "cidadao_promete_o_que_nao_existe",
            { Tipo: var t } when t.StartsWith("MQTT_", StringComparison.Ordinal) && !Cita(tecnico, "hmac", "segredo", "relógio") => "tecnico_sem_a_correcao_do_tipo",
            { Regra: "acesso-negado" } when !Cita(tecnico, "token", "login", "credencial", "correlationid") => "tecnico_sem_a_correcao_do_tipo",
            { Regra: "acesso-negado" } when Palavra().Matches(cidadao).Any(p => p.Value is "enviar" or "enviou" or "enviamos" or "tentamos") => "cidadao_contradiz_o_evento",
            _ => string.Empty,
        };
        return motivo.Length == 0;
    }

    private static bool SemAcento(string texto) => Palavra().Matches(texto.ToLowerInvariant()).Any(p => _semAcento.Contains(p.Value));

    private static bool Cita(string texto, params string[] termos) => termos.Any(t => texto.Contains(t, StringComparison.Ordinal));

    // Uma frase termina em pontuação seguida de espaço ou do fim: "art. 11" no meio não conta, nem os pontos de um número.
    [GeneratedRegex(@"[.!?]+(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex FimDeFrase();

    [GeneratedRegex(@"\p{L}+", RegexOptions.CultureInvariant)]
    private static partial Regex Palavra();
}
