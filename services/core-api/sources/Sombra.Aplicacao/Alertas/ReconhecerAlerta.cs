using System;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;

namespace Sombra.Aplicacao.Alertas;

/// <summary>Um cuidador responde pelo app: "vou ligar" e "estou indo" param o escalonamento; "ela está bem" encerra.</summary>
public sealed class ReconhecerAlerta
{
    private readonly IAlertaRepository _alertas;
    private readonly IRelogio _relogio;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="ReconhecerAlerta"/> class.</summary>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="relogio">O relógio.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public ReconhecerAlerta(IAlertaRepository alertas, IRelogio relogio, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _relogio = relogio ?? throw new ArgumentNullException(nameof(relogio));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Registra a resposta do cuidador.</summary>
    /// <param name="alertaId">O alerta.</param>
    /// <param name="dto">A ação.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>O alerta depois da resposta.</returns>
    /// <exception cref="ValidacaoException">Quando falta a ação.</exception>
    /// <exception cref="RecursoNaoEncontradoException">Quando o alerta não existe.</exception>
    public async Task<AlertaResposta> ExecutarAsync(Guid alertaId, ReconhecimentoDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        AlertaDeCalor alerta = await _alertas.ObterAsync(alertaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Alerta não encontrado.");

        alerta.Reconhecer(dto.Acao.GetValueOrDefault(), _relogio.Agora);
        if (!alerta.EstaAberto)
        {
            await _avisos.EncerramentoAsync(alerta, cancellationToken);
        }

        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return AlertaResposta.De(alerta);
    }
}
