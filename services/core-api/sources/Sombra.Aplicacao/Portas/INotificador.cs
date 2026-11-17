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

/// <summary>Um aviso a entregar. A mensagem nunca tem dado pessoal: no máximo o apelido da casa.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="AlertaId">O alerta que gerou o aviso.</param>
/// <param name="Destinatario">Para quem vai.</param>
/// <param name="Contatos">Os contatos que recebem; vazio quando o destinatário é o morador.</param>
/// <param name="Mensagem">O texto.</param>
public sealed record Notificacao(Guid DomicilioId, Guid AlertaId, Destinatario Destinatario, IReadOnlyList<ContatoDeCuidado> Contatos, string Mensagem);

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
