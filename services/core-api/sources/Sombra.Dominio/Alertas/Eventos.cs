using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Alertas;

/// <summary>Um alerta foi aberto.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Tipo">O tipo do alerta.</param>
/// <param name="Nivel">O nível de risco.</param>
/// <param name="Momento">Quando foi aberto.</param>
public sealed record AlertaAberto(Guid AlertaId, Guid DomicilioId, string Tipo, NivelDeRisco Nivel, DateTimeOffset Momento)
    : EventoDeDominio(Momento);

/// <summary>O nível de um alerta aberto subiu com uma leitura nova (regra R4).</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Nivel">O novo nível.</param>
/// <param name="Momento">Quando subiu.</param>
public sealed record AlertaAgravado(Guid AlertaId, Guid DomicilioId, NivelDeRisco Nivel, DateTimeOffset Momento)
    : EventoDeDominio(Momento);

/// <summary>O alerta subiu um degrau no escalonamento.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Degrau">O degrau alcançado.</param>
/// <param name="Momento">Quando escalonou.</param>
public sealed record AlertaEscalonado(Guid AlertaId, Guid DomicilioId, DegrauDeEscalonamento Degrau, DateTimeOffset Momento)
    : EventoDeDominio(Momento);

/// <summary>Um cuidador respondeu ao alerta.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Acao">A resposta do cuidador.</param>
/// <param name="Momento">Quando respondeu.</param>
public sealed record AlertaReconhecido(Guid AlertaId, Guid DomicilioId, AcaoDoCuidador Acao, DateTimeOffset Momento)
    : EventoDeDominio(Momento);

/// <summary>O alerta foi encerrado.</summary>
/// <param name="AlertaId">O alerta.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Estado">Como terminou (resolvido ou expirado).</param>
/// <param name="MinutosAteResolver">Quanto tempo levou desde a abertura, em minutos (indicador de impacto).</param>
/// <param name="Momento">Quando terminou.</param>
public sealed record AlertaEncerrado(Guid AlertaId, Guid DomicilioId, EstadoDoAlerta Estado, double MinutosAteResolver, DateTimeOffset Momento)
    : EventoDeDominio(Momento);
