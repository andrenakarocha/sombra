using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Sinks;

/// <summary>
/// Um vazamento que um guarda de sink impediu: o evento <c>VazamentoBloqueado{sink}</c> da spec 07. Só metadados: o sink,
/// onde (rota-modelo, evento de log, host ou tópico), a regra e a família, nunca o valor.
/// </summary>
/// <param name="Sink">Por onde o dado tentou sair.</param>
/// <param name="Onde">A rota-modelo, o nome do evento de log, o host ou o tópico.</param>
/// <param name="RegraId">A regra que reconheceu o dado.</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">O peso do vazamento.</param>
/// <param name="PeloTaint">Se foi o taint (o dado entrou na requisição) ou só a detecção por conteúdo.</param>
/// <param name="CorrelationId">A correlação da requisição ou da mensagem.</param>
public sealed record VazamentoBloqueado(
    SinkDePii Sink,
    string Onde,
    string RegraId,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    bool PeloTaint,
    string? CorrelationId);
