using System;
using System.Collections.Generic;
using System.Globalization;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;

namespace Sombra.Privacidade.Traducao;

/// <summary>
/// As traduções de template (<c>fonte: template</c>): o fallback da IA (sem chave, timeout, erro, resposta reprovada) e
/// o texto de todo evento até a IA responder. Cobrem as três audiências, por tipo, família e sink.
/// </summary>
public static class TemplatesDeRisco
{
    private static readonly Dictionary<string, string> _cidadao = new(StringComparer.Ordinal)
    {
        ["MQTT_ASSINATURA_INVALIDA"] = "Chegou uma mensagem com assinatura errada em nome da Sentinela da casa, e o Sombra descartou. Nenhum dado pessoal saiu.",
        ["MQTT_REPLAY"] = "Alguém tentou repetir uma mensagem antiga da Sentinela, e o Sombra recusou.",
        ["MQTT_FORA_DA_JANELA"] = "Chegou uma mensagem da Sentinela com o horário errado, e o Sombra recusou.",
        ["MQTT_MENSAGEM_INVALIDA"] = "A Sentinela mandou uma mensagem incompleta, e o Sombra ignorou.",
        ["ACESSO_NEGADO"] = "Alguém tentou ver {0} da casa sem permissão, e o Sombra bloqueou.",
        ["VAZAMENTO_BLOQUEADO"] = "O Sombra impediu que {0} da casa aparecesse onde não devia. Nada saiu.",
        ["SAIDA_EXTERNA_BLOQUEADA"] = "O Sombra impediu que {0} da casa fosse enviado para fora do sistema.",
        ["PII_FORA_DO_LUGAR"] = "Alguém escreveu {0} num campo de texto livre, e o Sombra escondeu antes de guardar.",
        ["VAZAMENTO_PERMITIDO"] = "Num teste com a proteção desligada, {0} da casa ficou exposto. O responsável pelo sistema já foi avisado.",
    };

    /// <summary>A mensagem para o cidadão.</summary>
    /// <param name="tipo">O tipo do evento.</param>
    /// <param name="categoria">A família do dado.</param>
    /// <returns>O texto.</returns>
    public static string Cidadao(string tipo, CategoriaDePii categoria)
        => string.Format(CultureInfo.InvariantCulture, _cidadao.GetValueOrDefault(tipo ?? string.Empty, "O Sombra barrou algo estranho ligado à casa. Nenhum dado pessoal saiu."), DaFamilia(categoria));

    /// <summary>As três traduções de template de um evento.</summary>
    /// <param name="evento">O evento.</param>
    /// <returns>A tradução, com <c>fonte: template</c>.</returns>
    public static TraducaoDeRisco Para(EventoParaTraduzir evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        string onde = PromptDeRisco.OndeSeguro(evento.Onde);
        string artigo = evento.Categoria == CategoriaDePii.Saude ? "LGPD art. 11" : "LGPD art. 46";
        string gestao = evento.Bloqueado
            ? $"Tentativa de exposição de {DaFamilia(evento.Categoria)} bloqueada ({artigo}); sem incidente a comunicar."
            : $"Incidente: {DaFamilia(evento.Categoria)} exposto; avaliar comunicação à ANPD e ao titular (LGPD art. 48).";
        string tecnico = evento switch
        {
            { Tipo: var t } when t.StartsWith("MQTT_", StringComparison.Ordinal) => $"Mensagem da Sentinela {onde} recusada ({evento.Tipo}): confira o segredo HMAC e o relógio do dispositivo.",
            { Regra: "acesso-negado" } => $"Acesso sem credencial a {onde}: confira se o cliente mandou o token; se for ataque, siga o correlationId na trilha.",
            { Sink: SinkDePii.Log } => $"O evento de log {onde} recebeu {evento.Regra}: use Sensivel<T> ou passe o valor pelo MascaradorDePii antes de logar.",
            { Sink: SinkDePii.RespostaHttp } => $"A resposta de {onde} levava {evento.Regra}: devolva o campo mascarado (MascaradorDePii) ou declare [PermitePii] só para o papel que precisa.",
            { Sink: SinkDePii.Excecao } => $"A mensagem de erro de {onde} foi montada com o dado ({evento.Regra}): não interpole valores de entrada em exceções.",
            { Sink: SinkDePii.SaidaExterna } => $"A chamada para {onde} levava {evento.Regra}: monte o payload só com metadados; nada de dado pessoal para fora.",
            { Sink: SinkDePii.Mqtt } => $"A publicação em {onde} levava {evento.Regra}: os tópicos MQTT só carregam ids e medições.",
            { Sink: SinkDePii.Entrada } => $"O campo livre de {onde} recebeu {evento.Regra}: ofereça um campo próprio para esse dado ou oriente quem preenche.",
            _ => $"Evento {evento.Tipo} em {onde}: revise o fluxo que levou o dado até ali.",
        };
        return new TraducaoDeRisco(Cidadao(evento.Tipo, evento.Categoria), gestao, tecnico, TraducaoDeRisco.FonteTemplate);
    }

    private static string DaFamilia(CategoriaDePii categoria) => categoria switch
    {
        CategoriaDePii.Saude => "um dado de saúde",
        CategoriaDePii.Identificacao => "um documento",
        CategoriaDePii.Contato => "um contato (telefone ou e-mail)",
        CategoriaDePii.Credencial => "uma senha ou chave de acesso",
        _ => "o endereço",
    };
}
