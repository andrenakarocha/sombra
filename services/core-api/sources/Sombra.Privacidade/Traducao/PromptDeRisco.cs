using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Deteccao;

namespace Sombra.Privacidade.Traducao;

/// <summary>
/// O prompt da tradução (spec 07 §3). Dinâmico, mas montado só com os campos do <see cref="EventoParaTraduzir"/>, e o
/// <c>onde</c> só entra se tiver forma de identificador (rota-modelo, nome de evento, host) e passar pelo motor de PII;
/// senão vira <c>(omitido)</c>. "Nem a IA vê o dado": o prompt nunca tem PII (P5), e o handler de saída confere de novo.
/// </summary>
public static partial class PromptDeRisco
{
    /// <summary>A instrução de sistema: o papel, as três audiências e o formato.</summary>
    public const string Sistema = """
        Você traduz eventos de segurança do Sombra, um sistema que protege pessoas idosas que moram sozinhas durante ondas de
        calor. O Sombra guarda dados de saúde, documentos e contatos, e um módulo de privacidade bloqueia qualquer saída
        indevida desses dados. Você recebe só metadados de um evento, nunca o dado. Não invente nomes, números, endereços nem
        detalhes que não estejam no evento.

        O que cada tipo de evento quer dizer:
        - ACESSO_NEGADO: alguém sem login ou sem permissão tentou ver um dado da casa (pela "regra" acesso-negado) e o Sombra
          negou. Nada foi mostrado. Correção técnica: conferir se o cliente mandou o token; se for ataque, seguir o
          correlationId na trilha.
        - VAZAMENTO_BLOQUEADO: o próprio sistema ia expor o dado num log, numa resposta ou numa mensagem de erro, e o Sombra
          bloqueou antes. Correção: Sensivel<T> ou MascaradorDePii no ponto indicado em "sink", ou [PermitePii] só para o
          papel que precisa.
        - SAIDA_EXTERNA_BLOQUEADA: o dado ia ser enviado para fora (outro serviço, IA, broker MQTT) e o Sombra barrou.
          Correção: montar o payload só com metadados.
        - PII_FORA_DO_LUGAR: alguém escreveu o dado num campo de texto livre e o Sombra escondeu antes de guardar. Correção:
          um campo próprio para o dado ou orientar quem preenche.
        - MQTT_ASSINATURA_INVALIDA, MQTT_REPLAY, MQTT_FORA_DA_JANELA, MQTT_MENSAGEM_INVALIDA: chegou uma mensagem falsa,
          repetida, com horário errado ou incompleta em nome da Sentinela (o aparelho de medir calor da casa), e o Sombra
          descartou. Não envolve dado pessoal. Correção técnica: conferir o segredo HMAC e o relógio do dispositivo; não use
          Sensivel<T> nem [PermitePii] nesse caso.
        Se "desfecho" for "vazou", o dado saiu de verdade: diga isso com calma e recomende avisar a família e o responsável.

        Responda só com JSON, com três campos em português do Brasil, com a ortografia correta (acentos e cedilha):
        - "cidadao": para a pessoa idosa ou a família. No máximo 2 frases curtas, tom calmo, sem termos técnicos (não use
          palavras como sink, log, token, endpoint, HTTP, MQTT, payload, LGPD). Diga o que aconteceu e se precisa fazer algo.
        - "gestao": para a gestão, uma linha com o impacto e o risco pela LGPD, citando o artigo (art. 11 para saúde,
          art. 46 para segurança, art. 48 se o dado vazou).
        - "tecnico": para o time técnico, a sugestão de correção: onde o dado tentou sair e que padrão do Sombra usar
          (Sensivel<T>, MascaradorDePii, [PermitePii] só para o papel que precisa, payload só com metadados).
        """;

    /// <summary>Monta o prompt de um evento.</summary>
    /// <param name="evento">O evento, só com metadados.</param>
    /// <returns>O texto do prompt.</returns>
    public static string Montar(EventoParaTraduzir evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        string metadados = JsonSerializer.Serialize(new
        {
            tipo = evento.Tipo,
            sink = evento.Sink?.ToString(),
            categoria = evento.Categoria.ToString(),
            severidade = evento.Severidade.ToString(),
            regra = evento.Regra,
            onde = OndeSeguro(evento.Onde),
            desfecho = evento.Bloqueado ? "bloqueado" : "vazou",
            repeticoesEm24h = evento.Repeticoes,
        });
        return $"Evento de segurança (só metadados): {metadados}";
    }

    /// <summary>O <c>onde</c> que pode ir para a IA: com forma de identificador e sem PII; senão, <c>(omitido)</c>.</summary>
    /// <param name="onde">A rota-modelo, o evento de log, o host ou o tópico.</param>
    /// <returns>O valor seguro.</returns>
    public static string OndeSeguro(string? onde)
        => onde is { Length: > 0 and <= 120 } && Identificador().IsMatch(onde) && !MotorDePii.Padrao.ContemPii(onde) ? onde : "(omitido)";

    [GeneratedRegex(@"^[a-z0-9{}:/._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Identificador();
}
