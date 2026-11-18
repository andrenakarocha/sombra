using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// A volta do escalonador (a cada 15 s): sobe os degraus que venceram (D13, caminho B), avisa quem cada degrau chama e
/// encerra os preventivos que passaram da validade.
/// </summary>
public sealed class EscalonarAlertas
{
    private readonly IDomicilioRepository _domicilios;
    private readonly IAlertaRepository _alertas;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="EscalonarAlertas"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public EscalonarAlertas(IDomicilioRepository domicilios, IAlertaRepository alertas, IRelogio relogio, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Faz uma volta.</summary>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>Quantos degraus subiram nesta volta.</returns>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _relogio.Agora;
        int degraus = 0;
        foreach (AlertaDeCalor alerta in await _alertas.ListarAbertosAsync(cancellationToken))
        {
            if (alerta is AlertaPreventivo preventivo)
            {
                preventivo.ExpirarSeVencido(agora);
                continue;
            }

            Domicilio? casa = await _domicilios.ObterAsync(alerta.DomicilioId, cancellationToken);
            if (casa is not null)
            {
                degraus += await EscalonarAsync(casa, alerta, agora, cancellationToken);
            }
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return degraus;
    }

    private async Task<int> EscalonarAsync(Domicilio casa, AlertaDeCalor alerta, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        int eventosAntes = alerta.EventosDeDominio.Count;
        alerta.Escalonar(agora, casa.RedeDeCuidado);
        AlertaEscalonado[] subidas = [.. alerta.EventosDeDominio.Skip(eventosAntes).OfType<AlertaEscalonado>()];
        foreach (AlertaEscalonado subida in subidas)
        {
            await _avisos.DoDegrauAsync(casa, alerta, subida.Degrau, cancellationToken);
        }

        if (subidas.Length > 0)
        {
            await _avisos.AoMoradorAsync(casa, alerta, MotivoDoAviso.Escalonamento, cancellationToken);
        }

        return subidas.Length;
    }
}
