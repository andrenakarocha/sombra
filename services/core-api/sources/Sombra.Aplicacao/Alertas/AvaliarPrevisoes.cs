using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// A volta do monitor de previsão: consulta o Clima uma vez por região, em paralelo, e abre o alerta preventivo nas casas
/// monitoradas onde a previsão de 24 h chega a perigo (roteiro, passo 2).
/// </summary>
public sealed class AvaliarPrevisoes
{
    private readonly IDomicilioRepository _domicilios;
    private readonly IAlertaRepository _alertas;
    private readonly IClimaGateway _clima;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="AvaliarPrevisoes"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="clima">O Clima Service.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public AvaliarPrevisoes(IDomicilioRepository domicilios, IAlertaRepository alertas, IClimaGateway clima, IRelogio relogio, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _clima = clima ?? throw new ArgumentNullException(nameof(clima));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Faz uma volta.</summary>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>Quantos alertas preventivos foram abertos.</returns>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Domicilio> casas = await _domicilios.ListarMonitoradosAsync(cancellationToken);
        string[] regioes = [.. casas.Select(c => c.RegiaoClimatica).Distinct(StringComparer.Ordinal)];
        RiscoDaRegiao?[] riscos = await Task.WhenAll(regioes.Select(r => _clima.ObterRiscoAsync(r, cancellationToken)));
        Dictionary<string, RiscoDaRegiao?> riscoDaRegiao = regioes.Zip(riscos).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal);

        DateTimeOffset agora = _relogio.Agora;
        int abertos = 0;
        foreach (Domicilio casa in casas)
        {
            if (riscoDaRegiao[casa.RegiaoClimatica] is { NivelPrevisto: >= NivelDeRisco.Perigo } risco
                && await AbrirSeLivreAsync(casa, risco.NivelPrevisto, agora, cancellationToken))
            {
                abertos++;
            }
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return abertos;
    }

    /// <summary>Só abre numa casa sem nenhum alerta aberto: um preventivo por vez, e nunca por cima de um que pede resposta.</summary>
    private async Task<bool> AbrirSeLivreAsync(Domicilio casa, NivelDeRisco nivelPrevisto, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        if ((await _alertas.ListarAbertosDaCasaAsync(casa.Id, cancellationToken)).Count > 0)
        {
            return false;
        }

        AlertaPreventivo alerta = AlertaPreventivo.Abrir(casa.Id, nivelPrevisto, agora);
        _alertas.Adicionar(alerta);
        await _avisos.AoMoradorAsync(casa, alerta, cancellationToken);
        await _avisos.DaPrevisaoAsync(casa, alerta, cancellationToken);
        return true;
    }
}
