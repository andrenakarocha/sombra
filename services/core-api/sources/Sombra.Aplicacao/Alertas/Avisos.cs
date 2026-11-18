using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// Monta os avisos de cada momento de um alerta e os entrega ao <see cref="INotificador"/>. Nas mensagens para a rede,
/// o único dado da casa é o apelido.
/// </summary>
internal sealed class Avisos
{
    private readonly INotificador _notificador;

    /// <summary>Initializes a new instance of the <see cref="Avisos"/> class.</summary>
    /// <param name="notificador">Quem entrega.</param>
    internal Avisos(INotificador notificador) => _notificador = notificador ?? throw new ArgumentNullException(nameof(notificador));

    /// <summary>Atualiza a tela da Sentinela e o Modo Idoso com a mensagem atual do alerta.</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta.</param>
    /// <param name="motivo">Por que a tela muda.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task AoMoradorAsync(Domicilio casa, AlertaDeCalor alerta, MotivoDoAviso motivo, CancellationToken cancellationToken)
        => EnviarAsync(casa.Id, alerta.Id, Destinatario.Morador, [], alerta.MensagemAoMorador, motivo, cancellationToken);

    /// <summary>O alerta terminou: a tela da Sentinela volta ao normal.</summary>
    /// <param name="alerta">O alerta encerrado.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task EncerramentoAsync(AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(alerta.DomicilioId, alerta.Id, Destinatario.Morador, [], "Tudo certo. Obrigado por responder.", MotivoDoAviso.AlertaEncerrado, cancellationToken);

    /// <summary>O lembrete de água do pico da tarde, só para a pessoa (R11).</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task LembreteDeAguaAsync(Domicilio casa, CancellationToken cancellationToken)
        => EnviarAsync(casa.Id, null, Destinatario.Morador, [], "Hora de beber um copo de água.", MotivoDoAviso.LembreteDeAgua, cancellationToken);

    /// <summary>O check-in diário esquecido: um lembrete na tela e um lembrete leve ao primeiro familiar, nunca um alerta (R11).</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal async Task LembreteDoCheckInAsync(Domicilio casa, CancellationToken cancellationToken)
    {
        await EnviarAsync(casa.Id, null, Destinatario.Morador, [], "Bom dia! Aperte o verde para dizer que está tudo bem.", MotivoDoAviso.CheckInDiario, cancellationToken);
        await EnviarAsync(casa.Id, null, Destinatario.Familiar, PrimeiroFamiliar(casa.RedeDeCuidado), $"{casa.Apelido}: o check-in de hoje ainda não veio. Vale ligar?", MotivoDoAviso.CheckInDiario, cancellationToken);
    }

    /// <summary>O aviso de um degrau do escalonamento (D13). O degrau 0 é só da Sentinela e não gera aviso à rede.</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta.</param>
    /// <param name="degrau">O degrau alcançado (1 a 3).</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task DoDegrauAsync(Domicilio casa, AlertaDeCalor alerta, DegrauDeEscalonamento degrau, CancellationToken cancellationToken)
    {
        RedeDeCuidado rede = casa.RedeDeCuidado;
        return degrau switch
        {
            DegrauDeEscalonamento.LembreteLeve => EnviarAsync(casa.Id, alerta.Id, Destinatario.Familiar, PrimeiroFamiliar(rede), $"{casa.Apelido}: o \"estou bem\" ainda não chegou. Quer ligar?", MotivoDoAviso.Escalonamento, cancellationToken),
            DegrauDeEscalonamento.QuemEstaPerto => EnviarAsync(casa.Id, alerta.Id, Destinatario.Vizinho, SoUm(rede.QuemEstaPerto), $"{casa.Apelido}: calor perigoso e ninguém respondeu. Pode dar uma olhada?", MotivoDoAviso.Escalonamento, cancellationToken),
            _ => EnviarAsync(casa.Id, alerta.Id, Destinatario.Rede, rede.Contatos, $"{casa.Apelido}: calor perigoso e sem resposta. Alguém precisa ir até lá.", MotivoDoAviso.Escalonamento, cancellationToken),
        };
    }

    /// <summary>O pedido de ajuda vai para a rede toda na hora (R7).</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task DoPedidoDeAjudaAsync(Domicilio casa, AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(casa.Id, alerta.Id, Destinatario.Rede, casa.RedeDeCuidado.Contatos, $"{casa.Apelido}: pedido de ajuda. Alguém precisa ir até lá agora.", MotivoDoAviso.PedidoDeAjuda, cancellationToken);

    /// <summary>A previsão de perigo vai para o primeiro familiar, para ele se preparar.</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta preventivo.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task DaPrevisaoAsync(Domicilio casa, AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(casa.Id, alerta.Id, Destinatario.Familiar, PrimeiroFamiliar(casa.RedeDeCuidado), $"{casa.Apelido}: previsão de calor perigoso nas próximas 24 h. Vale lembrar de beber água.", MotivoDoAviso.Previsao, cancellationToken);

    private static ContatoDeCuidado[] PrimeiroFamiliar(RedeDeCuidado rede) => SoUm(rede.PrimeiroFamiliar ?? (rede.Contatos.Count > 0 ? rede.Contatos[0] : null));

    private static ContatoDeCuidado[] SoUm(ContatoDeCuidado? contato) => contato is null ? [] : [contato];

    private Task EnviarAsync(Guid domicilioId, Guid? alertaId, Destinatario destinatario, IReadOnlyList<ContatoDeCuidado> contatos, string mensagem, MotivoDoAviso motivo, CancellationToken cancellationToken)
        => _notificador.NotificarAsync(new Notificacao(domicilioId, alertaId, destinatario, contatos, mensagem, motivo), cancellationToken);
}
