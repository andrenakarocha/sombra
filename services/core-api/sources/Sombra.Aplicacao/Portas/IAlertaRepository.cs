using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Alertas;

namespace Sombra.Aplicacao.Portas;

/// <summary>Os alertas de calor, de todos os tipos.</summary>
public interface IAlertaRepository
{
    /// <summary>Busca um alerta.</summary>
    /// <param name="id">O identificador.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O alerta, ou nulo.</returns>
    Task<AlertaDeCalor?> ObterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista os alertas abertos de uma casa.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os alertas abertos, do mais antigo ao mais novo.</returns>
    Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosDaCasaAsync(Guid domicilioId, CancellationToken cancellationToken);

    /// <summary>Busca o alerta encerrado mais recente de uma casa (para a carência da R13).</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O alerta, ou nulo.</returns>
    Task<AlertaDeCalor?> ObterUltimoEncerradoAsync(Guid domicilioId, CancellationToken cancellationToken);

    /// <summary>Lista todos os alertas abertos (para o escalonador).</summary>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os alertas abertos.</returns>
    Task<IReadOnlyList<AlertaDeCalor>> ListarAbertosAsync(CancellationToken cancellationToken);

    /// <summary>Inclui um alerta novo.</summary>
    /// <param name="alerta">O alerta.</param>
    void Adicionar(AlertaDeCalor alerta);
}
