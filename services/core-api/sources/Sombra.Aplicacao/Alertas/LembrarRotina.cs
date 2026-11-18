using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// A volta da rotina, junto com o escalonador: o lembrete de água no pico da tarde de um dia quente e o lembrete leve do
/// check-in diário esquecido (R11), nunca à noite (R12). Os lembretes de água ficam a 30 min um do outro, no máximo dois
/// por dia; o do check-in sai uma vez por dia.
/// </summary>
public sealed class LembrarRotina
{
    /// <summary>O intervalo mínimo entre dois lembretes de água.</summary>
    public static readonly TimeSpan IntervaloEntreLembretesDeAgua = TimeSpan.FromMinutes(30);

    private readonly IDomicilioRepository _domicilios;
    private readonly ILeituraRepository _leituras;
    private readonly ICheckInRepository _checkIns;
    private readonly IHistoricoDeAvisos _historico;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="LembrarRotina"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="leituras">As leituras.</param>
    /// <param name="checkIns">Os check-ins.</param>
    /// <param name="historico">Os avisos já enviados.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public LembrarRotina(IDomicilioRepository domicilios, ILeituraRepository leituras, ICheckInRepository checkIns, IHistoricoDeAvisos historico, IRelogio relogio, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _leituras = leituras ?? throw new ArgumentNullException(nameof(leituras));
        _checkIns = checkIns ?? throw new ArgumentNullException(nameof(checkIns));
        _historico = historico ?? throw new ArgumentNullException(nameof(historico));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Faz uma volta.</summary>
    /// <param name="cancellationToken">Cancela a volta.</param>
    /// <returns>Quantos lembretes saíram nesta volta.</returns>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset agora = _relogio.Agora;
        DateTimeOffset inicioDoDia = HorarioDeBrasilia.InicioDoDia(agora);
        TimeOnly horaLocal = TimeOnly.FromDateTime(HorarioDeBrasilia.Local(agora).DateTime);
        int lembretes = 0;
        foreach (Domicilio casa in await _domicilios.ListarMonitoradosAsync(cancellationToken))
        {
            if (await LembrarAguaAsync(casa, agora, inicioDoDia, horaLocal, cancellationToken))
            {
                lembretes++;
            }

            if (await LembrarCheckInAsync(casa, inicioDoDia, horaLocal, cancellationToken))
            {
                lembretes++;
            }
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return lembretes;
    }

    private async Task<bool> LembrarAguaAsync(Domicilio casa, DateTimeOffset agora, DateTimeOffset inicioDoDia, TimeOnly horaLocal, CancellationToken cancellationToken)
    {
        Leitura? ultima = await _leituras.ObterUltimaAsync(casa.Id, cancellationToken);
        NivelDeRisco nivelDoDia = ultima is not null && ultima.Momento >= inicioDoDia ? ultima.Nivel : NivelDeRisco.Normal;
        IReadOnlyList<DateTimeOffset> hoje = await _historico.MomentosAsync(casa.Id, MotivoDoAviso.LembreteDeAgua, inicioDoDia, cancellationToken);
        bool espacado = hoje.Count == 0 || agora - hoje[^1] >= IntervaloEntreLembretesDeAgua;
        if (!espacado || !casa.Rotina.DeveLembrarAgua(horaLocal, nivelDoDia, hoje.Count))
        {
            return false;
        }

        await _avisos.LembreteDeAguaAsync(casa, cancellationToken);
        return true;
    }

    private async Task<bool> LembrarCheckInAsync(Domicilio casa, DateTimeOffset inicioDoDia, TimeOnly horaLocal, CancellationToken cancellationToken)
    {
        CheckIn? ultimo = await _checkIns.ObterUltimoAsync(casa.Id, cancellationToken);
        bool jaFezHoje = ultimo is { Situacao: SituacaoDoCheckIn.EstouBem } && ultimo.Momento >= inicioDoDia;
        if (!casa.Rotina.CheckInDiarioEsquecido(horaLocal, jaFezHoje)
            || (await _historico.MomentosAsync(casa.Id, MotivoDoAviso.CheckInDiario, inicioDoDia, cancellationToken)).Count > 0)
        {
            return false;
        }

        await _avisos.LembreteDoCheckInAsync(casa, cancellationToken);
        return true;
    }
}
