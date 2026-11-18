using System;
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
    /// <param name="agora">O instante de referência, para saber quem está online.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Uma página de casas, por apelido.</returns>
    Task<Paginado<DomicilioResumoResposta>> ListarDomiciliosAsync(FiltroDeDomiciliosDto filtro, DateTimeOffset agora, CancellationToken cancellationToken);

    /// <summary>Lista os alertas.</summary>
    /// <param name="filtro">Estado, nível e paginação.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Uma página de alertas, do mais recente ao mais antigo.</returns>
    Task<Paginado<AlertaResposta>> ListarAlertasAsync(FiltroDeAlertasDto filtro, CancellationToken cancellationToken);

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
}
