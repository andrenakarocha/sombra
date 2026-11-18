using System;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>
/// O "estou bem" ou o "preciso de ajuda", do botão da Sentinela, do app ou de um cuidador. O "estou bem" encerra o alerta
/// que pede resposta (caminho A); o pedido de ajuda avisa a rede toda na hora (R7), no alerta que já estiver aberto (R4).
/// </summary>
public sealed class RegistrarCheckIn
{
    private readonly IDomicilioRepository _domicilios;
    private readonly IAlertaRepository _alertas;
    private readonly ILeituraRepository _leituras;
    private readonly ICheckInRepository _checkIns;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="RegistrarCheckIn"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="leituras">As leituras.</param>
    /// <param name="checkIns">Os check-ins.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public RegistrarCheckIn(IDomicilioRepository domicilios, IAlertaRepository alertas, ILeituraRepository leituras, ICheckInRepository checkIns, IRelogio relogio, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _leituras = leituras ?? throw new ArgumentNullException(nameof(leituras));
        _checkIns = checkIns ?? throw new ArgumentNullException(nameof(checkIns));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Registra o check-in.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="dto">A origem e a situação.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>O check-in e o alerta que ele encerrou ou abriu.</returns>
    /// <exception cref="ValidacaoException">Quando faltam campos.</exception>
    /// <exception cref="RecursoNaoEncontradoException">Quando a casa não existe.</exception>
    public async Task<CheckInResposta> ExecutarAsync(Guid domicilioId, NovoCheckInDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await _domicilios.ObterAsync(domicilioId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Domicílio não encontrado.");

        DateTimeOffset agora = _relogio.Agora;
        CheckIn checkIn = CheckIn.Registrar(casa.Id, agora, dto.Origem.GetValueOrDefault(), dto.Situacao.GetValueOrDefault());
        _checkIns.Adicionar(checkIn);

        AlertaDeCalor? aberto = await AlertasAbertos.QuePedeRespostaAsync(_alertas, casa.Id, cancellationToken);
        AlertaDeCalor? alerta = checkIn.Situacao == SituacaoDoCheckIn.EstouBem
            ? ConfirmarEstouBem(aberto, agora, checkIn.Origem)
            : await PedirAjudaAsync(casa, aberto, agora, cancellationToken);
        if (checkIn.Situacao == SituacaoDoCheckIn.EstouBem && alerta is not null)
        {
            await _avisos.EncerramentoAsync(alerta, cancellationToken);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return new CheckInResposta(checkIn.Id, checkIn.Momento, checkIn.Origem, checkIn.Situacao, alerta is null ? null : AlertaResposta.De(alerta, casa.Apelido));
    }

    private static AlertaDeCalor? ConfirmarEstouBem(AlertaDeCalor? aberto, DateTimeOffset agora, OrigemDoCheckIn origem)
    {
        aberto?.ConfirmarEstouBem(agora, origem);
        return aberto;
    }

    private async Task<AlertaDeCalor> PedirAjudaAsync(Domicilio casa, AlertaDeCalor? aberto, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        AlertaDeCalor alerta;
        if (aberto is null)
        {
            Leitura? ultima = await _leituras.ObterUltimaAsync(casa.Id, cancellationToken);
            alerta = AlertaDePedidoDeAjuda.Abrir(casa.Id, ultima?.Nivel ?? NivelDeRisco.Normal, agora);
            _alertas.Adicionar(alerta);
        }
        else if (aberto is AlertaDeSentinela deSentinela && deSentinela.Degrau < DegrauDeEscalonamento.AlertaParaARede)
        {
            deSentinela.PedirAjuda(agora);
            alerta = deSentinela;
        }
        else
        {
            // A rede toda já foi avisada por este alerta.
            return aberto;
        }

        await _avisos.DoPedidoDeAjudaAsync(casa, alerta, cancellationToken);
        await _avisos.AoMoradorAsync(casa, alerta, MotivoDoAviso.PedidoDeAjuda, cancellationToken);
        return alerta;
    }
}
