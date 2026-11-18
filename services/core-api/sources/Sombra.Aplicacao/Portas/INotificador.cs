using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Portas;

/// <summary>Para quem vai um aviso (o <c>destinatario</c> do contrato SignalR).</summary>
public enum Destinatario
{
    /// <summary>A pessoa monitorada, pela tela da Sentinela e pelo Modo Idoso do app.</summary>
    Morador = 0,

    /// <summary>O primeiro familiar da rede (degrau 1).</summary>
    Familiar = 1,

    /// <summary>Quem está perto (degrau 2).</summary>
    Vizinho = 2,

    /// <summary>A rede de cuidado inteira (degrau 3 e pedido de ajuda).</summary>
    Rede = 3,
}

/// <summary>Por que um aviso foi enviado (decide o comando da Sentinela e o evento do app).</summary>
public enum MotivoDoAviso
{
    /// <summary>Um alerta de Sentinela abriu.</summary>
    AlertaAberto = 0,

    /// <summary>O nível do alerta subiu.</summary>
    AlertaAgravado = 1,

    /// <summary>O alerta subiu um degrau (D13).</summary>
    Escalonamento = 2,

    /// <summary>A pessoa pediu ajuda (R7).</summary>
    PedidoDeAjuda = 3,

    /// <summary>A previsão de 24 h chegou a perigo.</summary>
    Previsao = 4,

    /// <summary>O alerta terminou: a tela da Sentinela volta ao normal.</summary>
    AlertaEncerrado = 5,

    /// <summary>Lembrete de água no pico da tarde (R11).</summary>
    LembreteDeAgua = 6,

    /// <summary>O check-in diário foi esquecido: lembrete leve (R11).</summary>
    CheckInDiario = 7,
}

/// <summary>Um aviso a entregar. A mensagem nunca tem dado pessoal: no máximo o apelido da casa.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="AlertaId">O alerta que gerou o aviso, quando há (lembretes da rotina não têm).</param>
/// <param name="Destinatario">Para quem vai.</param>
/// <param name="Contatos">Os contatos que recebem; vazio quando o destinatário é o morador.</param>
/// <param name="Mensagem">O texto (até 80 caracteres quando vai para a tela da Sentinela).</param>
/// <param name="Motivo">Por que o aviso foi enviado.</param>
public sealed record Notificacao(Guid DomicilioId, Guid? AlertaId, Destinatario Destinatario, IReadOnlyList<ContatoDeCuidado> Contatos, string Mensagem, MotivoDoAviso Motivo);

/// <summary>Quando os avisos de um tipo saíram para uma casa (o <c>LembrarRotina</c> respeita o máximo por dia).</summary>
public interface IHistoricoDeAvisos
{
    /// <summary>Os instantes dos avisos de um motivo desde um momento.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="motivo">O motivo.</param>
    /// <param name="desde">Desde quando.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os instantes, do mais antigo ao mais novo.</returns>
    Task<IReadOnlyList<DateTimeOffset>> MomentosAsync(Guid domicilioId, MotivoDoAviso motivo, DateTimeOffset desde, CancellationToken cancellationToken);
}

/// <summary>
/// Entrega avisos pelos canais (app, Sentinela, SMS simulado). A implementação grava no outbox, dentro da mesma unidade
/// de trabalho do caso de uso; quem envia de fato é o publicador de comandos.
/// </summary>
public interface INotificador
{
    /// <summary>Registra um aviso para entrega.</summary>
    /// <param name="notificacao">O aviso.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    Task NotificarAsync(Notificacao notificacao, CancellationToken cancellationToken);
}
