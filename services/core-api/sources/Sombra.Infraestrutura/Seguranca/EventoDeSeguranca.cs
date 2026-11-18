using System;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Risco;
using Sombra.Privacidade.Sinks;

namespace Sombra.Infraestrutura.Seguranca;

/// <summary>
/// Uma trilha de segurança: o que foi recusado, de onde e quando, só com metadados (nunca o payload nem dado pessoal).
/// Guarda as mensagens MQTT recusadas (A8) e os vazamentos que os guardas do <c>Sombra.Privacidade</c> bloquearam, com a
/// família, a severidade e a casa: é daqui que o farol do Risco Cidadão é calculado.
/// </summary>
public sealed class EventoDeSeguranca
{
    /// <summary>O tipo de um dado pessoal que entrou fora do lugar (campo livre).</summary>
    public const string PiiForaDoLugar = "PII_FORA_DO_LUGAR";

    /// <summary>O tipo de um vazamento barrado num sink interno (log, resposta, exceção).</summary>
    public const string VazamentoBarrado = "VAZAMENTO_BLOQUEADO";

    /// <summary>O tipo de uma saída externa barrada (HTTP para fora, MQTT).</summary>
    public const string SaidaExternaBarrada = "SAIDA_EXTERNA_BLOQUEADA";

    /// <summary>O tipo de um acesso negado a um endpoint com dado pessoal.</summary>
    public const string AcessoNegado = "ACESSO_NEGADO";

    private EventoDeSeguranca(Guid id, DateTimeOffset momento, string tipo, string origem, string? correlationId, string detalhe)
    {
        Id = id;
        Momento = momento;
        Tipo = tipo;
        Origem = origem;
        CorrelationId = correlationId;
        Detalhe = detalhe;
    }

    /// <summary>Gets o identificador.</summary>
    public Guid Id { get; }

    /// <summary>Gets quando aconteceu, em UTC.</summary>
    public DateTimeOffset Momento { get; }

    /// <summary>Gets o tipo, ex.: <c>MQTT_ASSINATURA_INVALIDA</c>.</summary>
    public string Tipo { get; }

    /// <summary>Gets de onde veio (o <c>deviceId</c>, para as mensagens MQTT).</summary>
    public string Origem { get; }

    /// <summary>Gets a correlação.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets o detalhe legível, sem dado pessoal.</summary>
    public string Detalhe { get; }

    /// <summary>Gets a casa envolvida, quando se sabe (a rota era de uma casa).</summary>
    public Guid? DomicilioId { get; private set; }

    /// <summary>Gets por onde o dado tentou sair (só nos eventos de privacidade).</summary>
    public SinkDePii? Sink { get; private set; }

    /// <summary>Gets a família do dado (só nos eventos de privacidade).</summary>
    public CategoriaDePii? Categoria { get; private set; }

    /// <summary>Gets a severidade da regra (só nos eventos de privacidade).</summary>
    public SeveridadeDePii? Severidade { get; private set; }

    /// <summary>Gets a regra que disparou (só nos eventos de privacidade).</summary>
    public string? Regra { get; private set; }

    /// <summary>Gets a value indicating whether o Sombra bloqueou (falso só no modo inseguro da demo).</summary>
    public bool Bloqueado { get; private set; } = true;

    /// <summary>Registra um evento.</summary>
    /// <param name="momento">Quando.</param>
    /// <param name="tipo">O tipo.</param>
    /// <param name="origem">De onde.</param>
    /// <param name="correlationId">A correlação.</param>
    /// <param name="detalhe">O detalhe, sem dado pessoal.</param>
    /// <returns>O evento.</returns>
    public static EventoDeSeguranca Registrar(DateTimeOffset momento, string tipo, string origem, string? correlationId, string detalhe)
        => new(Guid.CreateVersion7(), momento.ToUniversalTime(), tipo, origem, correlationId, detalhe);

    /// <summary>Registra um vazamento que um guarda bloqueou.</summary>
    /// <param name="vazamento">O evento do guarda, já com momento e casa.</param>
    /// <param name="agora">O momento, se o guarda não deu um.</param>
    /// <returns>O evento.</returns>
    public static EventoDeSeguranca DeVazamento(VazamentoBloqueado vazamento, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(vazamento);
        string tipo = vazamento switch
        {
            { Sink: SinkDePii.Entrada } => PiiForaDoLugar,
            { Sink: SinkDePii.SaidaExterna or SinkDePii.Mqtt } => SaidaExternaBarrada,
            { RegraId: "acesso-negado" } => AcessoNegado,
            _ => VazamentoBarrado,
        };
        string origem = vazamento.Onde.Length > 64 ? vazamento.Onde[..64] : vazamento.Onde;
        string detalhe = $"{vazamento.Sink}: {vazamento.Categoria} (regra {vazamento.RegraId}, {(vazamento.PeloTaint ? "taint" : "conteúdo")})";
        return new(Guid.CreateVersion7(), (vazamento.Momento ?? agora).ToUniversalTime(), tipo, origem, vazamento.CorrelationId, detalhe)
        {
            DomicilioId = vazamento.DomicilioId,
            Sink = vazamento.Sink,
            Categoria = vazamento.Categoria,
            Severidade = vazamento.Severidade,
            Regra = vazamento.RegraId,
            Bloqueado = vazamento.Bloqueado,
        };
    }

    /// <summary>
    /// O evento visto pelo farol. As mensagens MQTT recusadas contam como tentativa de se passar pela Sentinela
    /// (credencial, severidade alta; mensagem só malformada, baixa).
    /// </summary>
    /// <returns>O evento de risco.</returns>
    public EventoDeRisco ParaRisco()
        => new(
            Momento,
            Categoria ?? CategoriaDePii.Credencial,
            Severidade ?? (Tipo == "MQTT_MENSAGEM_INVALIDA" ? SeveridadeDePii.Baixa : SeveridadeDePii.Alta),
            Bloqueado);
}
