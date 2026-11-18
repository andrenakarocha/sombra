using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Respostas;

namespace Sombra.Aplicacao.Portas;

/// <summary>
/// As consultas de leitura do app e do painel. Não passam pelos agregados: projetam direto do banco, sem rastrear,
/// com filtro e paginação. Os filtros chegam já validados pela API.
/// </summary>
public interface IConsultasDoPainel
{
    /// <summary>Lista as casas, com o nível atual e o estado da Sentinela.</summary>
    /// <param name="filtro">Nível, bairro e paginação.</param>
    /// <param name="soEstasCasas">As casas que o Cuidador pode ver, ou nulo para todas (Operador e Admin).</param>
    /// <param name="agora">O instante de referência, para saber quem está online.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Uma página de casas, por apelido.</returns>
    Task<Paginado<DomicilioDetalheResposta>> ListarDomiciliosAsync(FiltroDeDomiciliosDto filtro, IReadOnlyCollection<Guid>? soEstasCasas, DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>Lista os alertas.</summary>
    /// <param name="filtro">Estado, nível e paginação.</param>
    /// <param name="soEstasCasas">As casas que o Cuidador pode ver, ou nulo para todas (Operador e Admin).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Uma página de alertas, do mais recente ao mais antigo.</returns>
    Task<Paginado<AlertaResposta>> ListarAlertasAsync(FiltroDeAlertasDto filtro, IReadOnlyCollection<Guid>? soEstasCasas, CancellationToken cancellationToken);

    /// <summary>Busca um alerta.</summary>
    /// <param name="id">O alerta.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O alerta, ou nulo.</returns>
    Task<AlertaResposta?> ObterAlertaAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista as leituras de uma casa num período.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="filtro">O período e a paginação.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Uma página de leituras, da mais recente à mais antiga.</returns>
    Task<Paginado<LeituraResposta>> ListarLeiturasAsync(Guid domicilioId, FiltroDeLeiturasDto filtro, CancellationToken cancellationToken);

    /// <summary>Os números do painel.</summary>
    /// <param name="agora">O instante de referência (hoje, semana, online).</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O resumo.</returns>
    Task<PainelResumoResposta> ObterResumoAsync(DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>O farol de privacidade da casa e as últimas mensagens, a partir dos eventos de segurança das Sentinelas dela.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="agora">O instante de referência.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A privacidade da casa.</returns>
    Task<PrivacidadeDaCasaResposta> PrivacidadeDaCasaAsync(Guid domicilioId, DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>O farol executivo de Risco Cidadão: todos os eventos de segurança da janela.</summary>
    /// <param name="agora">O instante de referência.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O farol global, por família, por sink e as regras que mais dispararam.</returns>
    Task<RiscoCidadaoResposta> RiscoCidadaoAsync(DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>A trilha de segurança, do mais recente para o mais antigo, só com metadados.</summary>
    /// <param name="filtro">A página.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>A página de eventos.</returns>
    Task<Paginado<EventoDeSegurancaResposta>> ListarEventosDeSegurancaAsync(ConsultaPaginadaDto filtro, CancellationToken cancellationToken);
}
