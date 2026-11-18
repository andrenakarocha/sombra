using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Risco;

namespace Sombra.Aplicacao.Respostas;

/// <summary>O horário de um ponto de resfriamento, <c>HH:mm</c> local.</summary>
/// <param name="Abre">Quando abre.</param>
/// <param name="Fecha">Quando fecha.</param>
public sealed record HorarioDeFuncionamentoResposta(string Abre, string Fecha);

/// <summary>Um lugar fresco perto da casa, no formato do app e do contrato do Clima.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Nome">O nome.</param>
/// <param name="Tipo">O tipo (<c>BIBLIOTECA</c>, <c>UBS</c>…).</param>
/// <param name="DistanciaKm">A distância em linha reta até a referência da casa, com uma casa decimal.</param>
/// <param name="Horario">O horário.</param>
/// <param name="Acessivel">Se tem acesso para quem usa cadeira de rodas ou andador.</param>
/// <param name="Fonte">De onde veio o dado.</param>
/// <param name="Lat">A latitude, para o "abrir rota".</param>
/// <param name="Lon">A longitude, para o "abrir rota".</param>
public sealed record PontoDeResfriamentoResposta(
    string Id,
    string Nome,
    string Tipo,
    double DistanciaKm,
    HorarioDeFuncionamentoResposta Horario,
    bool Acessivel,
    string Fonte,
    double Lat,
    double Lon);

/// <summary>O farol de privacidade da casa: cor, ícone e texto vêm daqui (D12).</summary>
public enum FarolDePrivacidade
{
    /// <summary>Nada estranho na última semana.</summary>
    Verde = 0,

    /// <summary>O Sombra barrou algo, e está tudo sob controle.</summary>
    Amarelo = 1,

    /// <summary>Várias tentativas barradas: vale a família olhar.</summary>
    Vermelho = 2,
}

/// <summary>Uma mensagem de privacidade para o cidadão, sem dado pessoal e sem jargão.</summary>
/// <param name="Momento">Quando aconteceu.</param>
/// <param name="MensagemCidadao">O que aconteceu, em linguagem simples.</param>
/// <param name="Fonte"><c>template</c> (texto fixo) ou <c>IA</c> (traduzida pela IA, na F2.6).</param>
public sealed record MensagemDePrivacidadeResposta(DateTimeOffset Momento, string MensagemCidadao, string Fonte);

/// <summary>
/// A privacidade da casa, no formato do app (D17). O farol é o Risco Cidadão (spec 07 §4: pesos, desfecho e decaimento)
/// dos eventos de segurança da casa: as mensagens recusadas das Sentinelas dela e os vazamentos que os guardas barraram
/// em rotas dela. As mensagens vêm de templates; a F2.6 traz a tradução da IA, sem mudar o formato.
/// </summary>
/// <param name="Farol">A cor do farol.</param>
/// <param name="Mensagens">As últimas mensagens, da mais recente para a mais antiga.</param>
public sealed record PrivacidadeDaCasaResposta(FarolDePrivacidade Farol, IReadOnlyList<MensagemDePrivacidadeResposta> Mensagens)
{
    /// <summary>A janela que o farol olha.</summary>
    public static readonly TimeSpan Janela = RiscoCidadao.Janela;

    /// <summary>Quantas mensagens a tela mostra.</summary>
    public const int MaximoDeMensagens = 5;

    private static readonly Dictionary<string, string> _textos = new(StringComparer.Ordinal)
    {
        ["MQTT_ASSINATURA_INVALIDA"] = "Chegou uma mensagem com assinatura errada em nome da Sentinela da casa, e o Sombra descartou. Nenhum dado pessoal saiu.",
        ["MQTT_REPLAY"] = "Alguém tentou repetir uma mensagem antiga da Sentinela, e o Sombra recusou.",
        ["MQTT_FORA_DA_JANELA"] = "Chegou uma mensagem da Sentinela com o horário errado, e o Sombra recusou.",
        ["MQTT_MENSAGEM_INVALIDA"] = "A Sentinela mandou uma mensagem incompleta, e o Sombra ignorou.",
        ["ACESSO_NEGADO"] = "Alguém tentou ver {0} da casa sem permissão, e o Sombra bloqueou.",
        ["VAZAMENTO_BLOQUEADO"] = "O Sombra impediu que {0} da casa aparecesse onde não devia. Nada saiu.",
        ["SAIDA_EXTERNA_BLOQUEADA"] = "O Sombra impediu que {0} da casa fosse enviado para fora do sistema.",
        ["PII_FORA_DO_LUGAR"] = "Alguém escreveu {0} num campo de texto livre, e o Sombra escondeu antes de guardar.",
    };

    /// <summary>Monta a privacidade a partir dos eventos de segurança da casa.</summary>
    /// <param name="eventos">Os eventos: o tipo e como o farol os vê (sem detalhe nenhum).</param>
    /// <param name="agora">O instante de referência.</param>
    /// <returns>A privacidade da casa.</returns>
    public static PrivacidadeDaCasaResposta De(IEnumerable<(string Tipo, EventoDeRisco Risco)> eventos, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        (string Tipo, EventoDeRisco Risco)[] recentes = [.. eventos.Where(e => e.Risco.Momento >= agora - Janela).OrderByDescending(e => e.Risco.Momento)];
        return new(
            Farois.De(RiscoCidadao.Avaliar(recentes.Select(e => e.Risco), agora).Farol),
            [.. recentes.Take(MaximoDeMensagens).Select(e => new MensagemDePrivacidadeResposta(e.Risco.Momento, Texto(e.Tipo, e.Risco.Categoria), "template"))]);
    }

    private static string Texto(string tipo, CategoriaDePii categoria)
        => string.Format(
            CultureInfo.InvariantCulture,
            _textos.GetValueOrDefault(tipo, "O Sombra barrou algo estranho ligado à casa. Nenhum dado pessoal saiu."),
            categoria switch
            {
                CategoriaDePii.Saude => "um dado de saúde",
                CategoriaDePii.Identificacao => "um documento",
                CategoriaDePii.Contato => "um contato (telefone ou e-mail)",
                CategoriaDePii.Credencial => "uma senha ou chave de acesso",
                _ => "o endereço",
            });
}
