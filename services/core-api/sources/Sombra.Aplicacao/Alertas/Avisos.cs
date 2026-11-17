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
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task AoMoradorAsync(Domicilio casa, AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(casa, alerta, Destinatario.Morador, [], alerta.MensagemAoMorador, cancellationToken);

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
            DegrauDeEscalonamento.LembreteLeve => EnviarAsync(casa, alerta, Destinatario.Familiar, PrimeiroFamiliar(rede), $"{casa.Apelido}: o \"estou bem\" ainda não chegou. Quer ligar?", cancellationToken),
            DegrauDeEscalonamento.QuemEstaPerto => EnviarAsync(casa, alerta, Destinatario.Vizinho, SoUm(rede.QuemEstaPerto), $"{casa.Apelido}: calor perigoso e ninguém respondeu. Pode dar uma olhada?", cancellationToken),
            _ => EnviarAsync(casa, alerta, Destinatario.Rede, rede.Contatos, $"{casa.Apelido}: calor perigoso e sem resposta. Alguém precisa ir até lá.", cancellationToken),
        };
    }

    /// <summary>O pedido de ajuda vai para a rede toda na hora (R7).</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task DoPedidoDeAjudaAsync(Domicilio casa, AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(casa, alerta, Destinatario.Rede, casa.RedeDeCuidado.Contatos, $"{casa.Apelido}: pedido de ajuda. Alguém precisa ir até lá agora.", cancellationToken);

    /// <summary>A previsão de perigo vai para o primeiro familiar, para ele se preparar.</summary>
    /// <param name="casa">A casa.</param>
    /// <param name="alerta">O alerta preventivo.</param>
    /// <param name="cancellationToken">Cancela o registro.</param>
    /// <returns>O registro.</returns>
    internal Task DaPrevisaoAsync(Domicilio casa, AlertaDeCalor alerta, CancellationToken cancellationToken)
        => EnviarAsync(casa, alerta, Destinatario.Familiar, PrimeiroFamiliar(casa.RedeDeCuidado), $"{casa.Apelido}: previsão de calor perigoso nas próximas 24 h. Vale lembrar de beber água.", cancellationToken);

    private static ContatoDeCuidado[] PrimeiroFamiliar(RedeDeCuidado rede) => SoUm(rede.PrimeiroFamiliar ?? (rede.Contatos.Count > 0 ? rede.Contatos[0] : null));

    private static ContatoDeCuidado[] SoUm(ContatoDeCuidado? contato) => contato is null ? [] : [contato];

    private Task EnviarAsync(Domicilio casa, AlertaDeCalor alerta, Destinatario destinatario, IReadOnlyList<ContatoDeCuidado> contatos, string mensagem, CancellationToken cancellationToken)
        => _notificador.NotificarAsync(new Notificacao(casa.Id, alerta.Id, destinatario, contatos, mensagem), cancellationToken);
}
