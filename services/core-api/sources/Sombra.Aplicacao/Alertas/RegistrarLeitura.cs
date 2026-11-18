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
/// A telemetria da Sentinela (MQTT ou POST assinado): guarda a leitura e, numa casa monitorada, abre o alerta a partir de
/// cautela extrema ou atualiza o que já está aberto (R4, R6, R9), respeitando a carência depois do "estou bem" (R13).
/// </summary>
public sealed class RegistrarLeitura
{
    private readonly IDomicilioRepository _domicilios;
    private readonly IAlertaRepository _alertas;
    private readonly ILeituraRepository _leituras;
    private readonly IUnitOfWork _unidadeDeTrabalho;
    private readonly Avisos _avisos;

    /// <summary>Initializes a new instance of the <see cref="RegistrarLeitura"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="alertas">Os alertas.</param>
    /// <param name="leituras">As leituras.</param>
    /// <param name="notificador">Quem entrega os avisos.</param>
    /// <param name="unidadeDeTrabalho">A transação.</param>
    public RegistrarLeitura(IDomicilioRepository domicilios, IAlertaRepository alertas, ILeituraRepository leituras, INotificador notificador, IUnitOfWork unidadeDeTrabalho)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _alertas = alertas ?? throw new ArgumentNullException(nameof(alertas));
        _leituras = leituras ?? throw new ArgumentNullException(nameof(leituras));
        _unidadeDeTrabalho = unidadeDeTrabalho ?? throw new ArgumentNullException(nameof(unidadeDeTrabalho));
        _avisos = new Avisos(notificador);
    }

    /// <summary>Registra a leitura.</summary>
    /// <param name="dto">A medição.</param>
    /// <param name="cancellationToken">Cancela o caso de uso.</param>
    /// <returns>A leitura classificada e o alerta afetado.</returns>
    /// <exception cref="ValidacaoException">Quando faltam campos ou o identificador não segue o contrato.</exception>
    /// <exception cref="RecursoNaoEncontradoException">Quando a Sentinela não está vinculada a nenhuma casa.</exception>
    public async Task<LeituraRegistradaResposta> ExecutarAsync(NovaLeituraDto dto, CancellationToken cancellationToken)
    {
        Validacao.Garantir(dto);
        Domicilio casa = await _domicilios.ObterPorSentinelaAsync(dto.DeviceId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Sentinela não vinculada a nenhuma casa.");

        Leitura leitura = casa.RegistrarLeitura(
            dto.DeviceId,
            dto.Momento.GetValueOrDefault(),
            Temperatura.Criar(dto.TemperaturaC.GetValueOrDefault()),
            UmidadeRelativa.Criar(dto.UmidadePct.GetValueOrDefault()));
        _leituras.Adicionar(casa.Id, leitura);

        AlertaDeCalor? alerta = casa.Monitorado ? await AbrirOuAtualizarAsync(casa, leitura, cancellationToken) : null;
        await _unidadeDeTrabalho.SalvarAsync(cancellationToken);
        return new LeituraRegistradaResposta(LeituraResposta.De(leitura), alerta is null ? null : AlertaResposta.De(alerta, casa.Apelido));
    }

    private async Task<AlertaDeCalor?> AbrirOuAtualizarAsync(Domicilio casa, Leitura leitura, CancellationToken cancellationToken)
    {
        AlertaDeCalor? aberto = await AlertasAbertos.QuePedeRespostaAsync(_alertas, casa.Id, cancellationToken);
        switch (aberto)
        {
            case AlertaDeSentinela deSentinela:
                NivelDeRisco antes = deSentinela.Nivel;
                deSentinela.AtualizarComLeitura(leitura);
                if (deSentinela.Nivel > antes)
                {
                    await _avisos.AoMoradorAsync(casa, deSentinela, MotivoDoAviso.AlertaAgravado, cancellationToken);
                }

                return deSentinela;

            case null when leitura.Nivel >= NivelDeRisco.CautelaExtrema
                && !AlertaDeSentinela.EmCarencia(await _alertas.ObterUltimoEncerradoAsync(casa.Id, cancellationToken), leitura):
                AlertaDeSentinela novo = AlertaDeSentinela.Abrir(casa.Id, leitura);
                _alertas.Adicionar(novo);
                await _avisos.AoMoradorAsync(casa, novo, MotivoDoAviso.AlertaAberto, cancellationToken);
                return novo;

            default:
                return aberto;
        }
    }
}
