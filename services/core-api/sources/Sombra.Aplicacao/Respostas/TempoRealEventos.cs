using System;
using Sombra.Aplicacao.Portas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Respostas;

/// <summary>Os nomes dos eventos do hub (<c>docs/contratos/signalr.md</c>).</summary>
public static class EventosDeTempoReal
{
    /// <summary>Uma leitura nova da Sentinela.</summary>
    public const string LeituraRegistrada = nameof(LeituraRegistrada);

    /// <summary>A Sentinela ficou online ou offline.</summary>
    public const string SentinelaStatus = nameof(SentinelaStatus);

    /// <summary>Um alerta abriu.</summary>
    public const string AlertaAberto = nameof(AlertaAberto);

    /// <summary>Um alerta subiu um degrau (D13) ou virou pedido de ajuda.</summary>
    public const string AlertaEscalonado = nameof(AlertaEscalonado);

    /// <summary>Um alerta foi resolvido.</summary>
    public const string AlertaResolvido = nameof(AlertaResolvido);

    /// <summary>Um check-in foi registrado.</summary>
    public const string CheckInRegistrado = nameof(CheckInRegistrado);

    /// <summary>Um aviso para a rede que não é escalonamento (previsão, lembrete do check-in diário).</summary>
    public const string AvisoParaARede = nameof(AvisoParaARede);

    /// <summary>Um alerta de infraestrutura do monitor da VM (só operadores).</summary>
    public const string AlertaDeInfra = nameof(AlertaDeInfra);

    /// <summary>O farol de Risco Cidadão mudou porque entrou um evento de segurança (só operadores, farol global).</summary>
    public const string RiscoCidadaoAtualizado = nameof(RiscoCidadaoAtualizado);
}

/// <summary>Evento <c>LeituraRegistrada</c>.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Momento">O instante da medição.</param>
/// <param name="TemperaturaC">A temperatura, em °C.</param>
/// <param name="UmidadePct">A umidade, em %.</param>
/// <param name="IndiceDeCalorC">O índice de calor, em °C.</param>
/// <param name="Nivel">O nível pelos limites da casa.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record LeituraRegistradaEvento(Guid DomicilioId, DateTimeOffset Momento, double TemperaturaC, double UmidadePct, double IndiceDeCalorC, NivelDeRisco Nivel, string? CorrelationId);

/// <summary>Evento <c>SentinelaStatus</c>.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="DeviceId">A Sentinela.</param>
/// <param name="Online">Se está online.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record SentinelaStatusEvento(Guid DomicilioId, string DeviceId, bool Online, string? CorrelationId);

/// <summary>Evento <c>AlertaAberto</c>.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Tipo">O tipo.</param>
/// <param name="Nivel">O nível.</param>
/// <param name="Prazo">O prazo do "estou bem", quando há.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record AlertaAbertoEvento(Guid AlertaId, Guid DomicilioId, string Tipo, NivelDeRisco Nivel, DateTimeOffset? Prazo, string? CorrelationId);

/// <summary>Evento <c>AlertaEscalonado</c>.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Degrau">O degrau, de 1 a 3.</param>
/// <param name="Destinatario">Quem foi avisado: <c>FAMILIAR</c>, <c>VIZINHO</c> ou <c>REDE</c>.</param>
/// <param name="Mensagem">O texto do aviso (com o apelido da casa, sem dado pessoal).</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record AlertaEscalonadoEvento(Guid AlertaId, Guid DomicilioId, int Degrau, Destinatario Destinatario, string Mensagem, string? CorrelationId);

/// <summary>Evento <c>AlertaResolvido</c>.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="ResolvidoPor"><c>CHECKIN_SENTINELA</c>, <c>CHECKIN_APP</c> ou <c>CUIDADOR</c>.</param>
/// <param name="MinutosAteCheckIn">Quanto tempo levou desde a abertura.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record AlertaResolvidoEvento(Guid AlertaId, Guid DomicilioId, string ResolvidoPor, double MinutosAteCheckIn, string? CorrelationId);

/// <summary>Evento <c>CheckInRegistrado</c>.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Momento">Quando.</param>
/// <param name="Origem">De onde veio.</param>
/// <param name="Situacao">O que a pessoa disse.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record CheckInRegistradoEvento(Guid DomicilioId, DateTimeOffset Momento, OrigemDoCheckIn Origem, SituacaoDoCheckIn Situacao, string? CorrelationId);

/// <summary>Evento <c>AvisoParaARede</c>.</summary>
/// <param name="DomicilioId">A casa.</param>
/// <param name="AlertaId">O alerta, quando há.</param>
/// <param name="Destinatario">Quem foi avisado.</param>
/// <param name="Motivo">Por quê.</param>
/// <param name="Mensagem">O texto do aviso.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record AvisoParaARedeEvento(Guid DomicilioId, Guid? AlertaId, Destinatario Destinatario, MotivoDoAviso Motivo, string Mensagem, string? CorrelationId);

/// <summary>Evento <c>AlertaDeInfra</c>.</summary>
/// <param name="Host">O servidor.</param>
/// <param name="Recurso">CPU, memória, disco ou serviço.</param>
/// <param name="Severidade">AVISO ou CRITICO.</param>
/// <param name="Mensagem">O que o monitor viu.</param>
/// <param name="Momento">Quando.</param>
/// <param name="CorrelationId">A correlação.</param>
public sealed record AlertaDeInfraEvento(string Host, string Recurso, string Severidade, string Mensagem, DateTimeOffset Momento, string? CorrelationId);

/// <summary>Evento <c>RiscoCidadaoAtualizado</c> (farol global: <c>DomicilioId</c> nulo).</summary>
/// <param name="DomicilioId">A casa, ou nulo para o farol global.</param>
/// <param name="Farol">A cor.</param>
/// <param name="Pontuacao">A pontuação de 0 a 100.</param>
/// <param name="EventosNaJanela">Quantos eventos entraram na conta.</param>
/// <param name="MensagemCidadao">Uma frase para quem não é técnico, sem dado pessoal.</param>
/// <param name="CorrelationId">A correlação do lote que mudou o farol.</param>
public sealed record RiscoCidadaoAtualizadoEvento(Guid? DomicilioId, FarolDePrivacidade Farol, double Pontuacao, int EventosNaJanela, string MensagemCidadao, string? CorrelationId);
