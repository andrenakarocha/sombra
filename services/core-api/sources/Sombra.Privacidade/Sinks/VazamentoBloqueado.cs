using System;
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
/// <param name="DomicilioId">A casa da requisição, quando a rota é de uma casa (preenchida pela fila).</param>
/// <param name="Momento">Quando foi bloqueado (preenchido pela fila, no relógio do sistema).</param>
/// <param name="Bloqueado">Se foi bloqueado (sempre, com o módulo ligado) ou só registrado (modo inseguro da demo, F2.8).</param>
public sealed record VazamentoBloqueado(
    SinkDePii Sink,
    string Onde,
    string RegraId,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    bool PeloTaint,
    string? CorrelationId,
    Guid? DomicilioId = null,
    DateTimeOffset? Momento = null,
    bool Bloqueado = true);
