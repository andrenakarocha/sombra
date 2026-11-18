using System;
using System.Collections.Generic;
using Sombra.Dominio.Calor;

namespace Sombra.Aplicacao.Respostas;

/// <summary>Uma página de resultados (contrato: <c>itens</c>, <c>pagina</c>, <c>tamanho</c>, <c>total</c>).</summary>
/// <typeparam name="T">O tipo do item.</typeparam>
/// <param name="Itens">Os itens desta página.</param>
/// <param name="Pagina">A página, a partir de 1.</param>
/// <param name="Tamanho">O tamanho pedido.</param>
/// <param name="Total">Quantos itens existem com estes filtros.</param>
public sealed record Paginado<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int Total);

/// <summary>Uma casa na lista do painel, sem dado pessoal além do apelido e do bairro.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Apelido">Como a família chama a casa.</param>
/// <param name="Bairro">O bairro.</param>
/// <param name="Monitorado">Se o monitoramento está ligado.</param>
/// <param name="NivelAtual">O nível da última leitura, ou normal sem leitura.</param>
/// <param name="SentinelaOnline">Se a Sentinela falou nos últimos 30 segundos (R9).</param>
/// <param name="AlertaAberto">Se há algum alerta aberto.</param>
public sealed record DomicilioResumoResposta(Guid Id, string Apelido, string Bairro, bool Monitorado, NivelDeRisco NivelAtual, bool SentinelaOnline, bool AlertaAberto);

/// <summary>Os números do painel e os indicadores de impacto (contrato <c>GET /painel/resumo</c>).</summary>
/// <param name="CasasMonitoradas">Quantas casas têm o monitoramento ligado.</param>
/// <param name="AlertasAtivos">Quantos alertas estão abertos.</param>
/// <param name="TempoMedianoAteCheckInMin">Na semana, a mediana dos minutos entre a abertura e o "estou bem" nos alertas de Sentinela resolvidos.</param>
/// <param name="AlertasResolvidosSemUltimoDegrauPct">Na semana, quantos por cento dos alertas de Sentinela resolvidos terminaram antes de chamar a rede toda.</param>
/// <param name="AlarmesDesnecessariosSemana">Na semana, alertas que chamaram a rede toda e terminaram com o cuidador dizendo que estava tudo bem.</param>
/// <param name="EscalonamentosHojePorDegrau">Os alertas abertos hoje (horário local) pelo degrau mais alto que alcançaram, de 1 a 3.</param>
/// <param name="SentinelasOffline">Sentinelas de casas monitoradas sem contato nos últimos 30 segundos.</param>
/// <param name="AlertasAtivosPorNivel">Os alertas abertos por nível de risco.</param>
/// <param name="AlertasDaSemanaPorBairro">Os alertas abertos nos últimos 7 dias, por bairro.</param>
public sealed record PainelResumoResposta(
    int CasasMonitoradas,
    int AlertasAtivos,
    double? TempoMedianoAteCheckInMin,
    double? AlertasResolvidosSemUltimoDegrauPct,
    int AlarmesDesnecessariosSemana,
    IReadOnlyDictionary<int, int> EscalonamentosHojePorDegrau,
    int SentinelasOffline,
    IReadOnlyDictionary<NivelDeRisco, int> AlertasAtivosPorNivel,
    IReadOnlyDictionary<string, int> AlertasDaSemanaPorBairro);
