using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>O que uma gravação acabou de tornar verdade: leituras e check-ins novos e os eventos dos alertas.</summary>
/// <param name="Leituras">As leituras novas, com a casa.</param>
/// <param name="CheckIns">Os check-ins novos.</param>
/// <param name="Alertas">Os alertas que mudaram, com os eventos de domínio deles.</param>
public sealed record FatosGravados(
    IReadOnlyList<(Leitura Leitura, Guid DomicilioId)> Leituras,
    IReadOnlyList<CheckIn> CheckIns,
    IReadOnlyList<(AlertaDeCalor Alerta, IReadOnlyList<EventoDeDominio> Eventos)> Alertas);

/// <summary>
/// Depois do commit, conta o que aconteceu: uma linha de log por fato (só IDs) e o evento de tempo real do contrato
/// SignalR, com a correlação de quem causou. Nada sai antes de gravado.
/// </summary>
public sealed class DespachanteDeFatos
{
    private readonly ILogger<DespachanteDeFatos> _log;
    private readonly ITempoReal _tempoReal;

    /// <summary>Initializes a new instance of the <see cref="DespachanteDeFatos"/> class.</summary>
    /// <param name="log">O log.</param>
    /// <param name="tempoReal">O hub (ou o desligado, fora da API).</param>
    public DespachanteDeFatos(ILogger<DespachanteDeFatos> log, ITempoReal tempoReal)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _tempoReal = tempoReal ?? throw new ArgumentNullException(nameof(tempoReal));
    }

    /// <summary>Registra e publica os fatos.</summary>
    /// <param name="fatos">Os fatos gravados.</param>
    /// <param name="cancellationToken">Cancela a publicação.</param>
    /// <returns>O despacho.</returns>
    public async Task DespacharAsync(FatosGravados fatos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fatos);
        string? correlacao = Correlacao.Atual;
        foreach ((Leitura leitura, Guid casa) in fatos.Leituras)
        {
            _log.LeituraRegistrada(leitura.DeviceId, casa, leitura.Nivel);
            await _tempoReal.PublicarAsync(EventosDeTempoReal.LeituraRegistrada, casa, new LeituraRegistradaEvento(
                casa, leitura.Momento, leitura.Temperatura.Celsius, leitura.Umidade.Percentual, Math.Round(leitura.IndiceDeCalor.Celsius, 2), leitura.Nivel, correlacao), cancellationToken);
        }

        foreach (CheckIn checkIn in fatos.CheckIns)
        {
            _log.CheckInRegistrado(checkIn.Situacao, checkIn.Origem, checkIn.DomicilioId);
            if (checkIn.Situacao == SituacaoDoCheckIn.PrecisoDeAjuda)
            {
                _log.PedidoDeAjudaRecebido(checkIn.DomicilioId, checkIn.Origem);
            }

            await _tempoReal.PublicarAsync(EventosDeTempoReal.CheckInRegistrado, checkIn.DomicilioId, new CheckInRegistradoEvento(
                checkIn.DomicilioId, checkIn.Momento, checkIn.Origem, checkIn.Situacao, correlacao), cancellationToken);
        }

        foreach ((AlertaDeCalor alerta, IReadOnlyList<EventoDeDominio> eventos) in fatos.Alertas)
        {
            foreach (EventoDeDominio evento in eventos)
            {
                await DespacharAsync(alerta, evento, fatos.CheckIns, correlacao, cancellationToken);
            }
        }
    }

    private static string ResolvidoPor(AlertaDeCalor alerta, IReadOnlyList<CheckIn> checkIns)
    {
        if (alerta.Reconhecimento == AcaoDoCuidador.ElaEstaBem)
        {
            return "CUIDADOR";
        }

        return checkIns.LastOrDefault(c => c.DomicilioId == alerta.DomicilioId)?.Origem switch
        {
            OrigemDoCheckIn.BotaoSentinela => "CHECKIN_SENTINELA",
            OrigemDoCheckIn.Cuidador => "CUIDADOR",
            _ => "CHECKIN_APP",
        };
    }

    private async Task DespacharAsync(AlertaDeCalor alerta, EventoDeDominio evento, IReadOnlyList<CheckIn> checkIns, string? correlacao, CancellationToken cancellationToken)
    {
        switch (evento)
        {
            case AlertaAberto a:
                _log.AlertaAberto(a.AlertaId, a.Tipo, a.DomicilioId, a.Nivel);
                await _tempoReal.PublicarAsync(EventosDeTempoReal.AlertaAberto, a.DomicilioId, new AlertaAbertoEvento(a.AlertaId, a.DomicilioId, a.Tipo, a.Nivel, alerta.Prazo, correlacao), cancellationToken);
                break;
            case AlertaAgravado a:
                _log.AlertaAgravado(a.AlertaId, a.DomicilioId, a.Nivel);
                break;
            case AlertaEscalonado a:
                _log.AlertaEscalonado(a.AlertaId, a.DomicilioId, (int)a.Degrau);
                break;
            case AlertaReconhecido a:
                _log.AlertaReconhecido(a.AlertaId, a.DomicilioId, a.Acao);
                break;
            case AlertaEncerrado { Estado: EstadoDoAlerta.Expirado } a:
                _log.AlertaExpirado(a.AlertaId, a.DomicilioId);
                break;
            case AlertaEncerrado a:
                double minutos = Math.Round(a.MinutosAteResolver, 1);
                _log.AlertaResolvido(a.AlertaId, a.DomicilioId, minutos);
                await _tempoReal.PublicarAsync(EventosDeTempoReal.AlertaResolvido, a.DomicilioId, new AlertaResolvidoEvento(a.AlertaId, a.DomicilioId, ResolvidoPor(alerta, checkIns), minutos, correlacao), cancellationToken);
                break;
        }
    }
}

/// <summary>Tempo real desligado: fora da API (migrações, testes de repositório) não há hub.</summary>
public sealed class TempoRealDesligado : ITempoReal
{
    /// <inheritdoc/>
    public Task PublicarAsync(string evento, Guid? domicilioId, object dados, CancellationToken cancellationToken) => Task.CompletedTask;
}
