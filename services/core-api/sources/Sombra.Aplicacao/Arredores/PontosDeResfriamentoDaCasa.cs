using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Comum;
using Sombra.Aplicacao.Dtos;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Geo;

namespace Sombra.Aplicacao.Arredores;

/// <summary>
/// Os pontos de resfriamento perto da casa, para a tela "Perto de mim" do app. A casa ainda não tem coordenada: a
/// referência é a estação do INMET da região climática dela, a mesma que o modelo e o Clima usam.
/// </summary>
public sealed class PontosDeResfriamentoDaCasa
{
    // Coordenadas do cabeçalho dos CSVs do INMET (portal de dados históricos): estação A701, Mirante de Santana.
    private static readonly Dictionary<string, PontoGeografico> _referencias = new(StringComparer.Ordinal)
    {
        ["SP-MIRANTE"] = PontoGeografico.Criar(-23.4964, -46.6200),
    };

    private readonly IDomicilioRepository _domicilios;
    private readonly IPontosDeResfriamento _pontos;

    /// <summary>Initializes a new instance of the <see cref="PontosDeResfriamentoDaCasa"/> class.</summary>
    /// <param name="domicilios">As casas.</param>
    /// <param name="pontos">Os pontos de resfriamento.</param>
    public PontosDeResfriamentoDaCasa(IDomicilioRepository domicilios, IPontosDeResfriamento pontos)
    {
        _domicilios = domicilios ?? throw new ArgumentNullException(nameof(domicilios));
        _pontos = pontos ?? throw new ArgumentNullException(nameof(pontos));
    }

    /// <summary>Os pontos dentro do raio, do mais perto ao mais longe; vazio quando a região não tem referência.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="filtro">O raio.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>Os pontos.</returns>
    /// <exception cref="RecursoNaoEncontradoException">Quando a casa não existe.</exception>
    public async Task<IReadOnlyList<PontoDeResfriamentoResposta>> ExecutarAsync(Guid domicilioId, FiltroDePontosDto filtro, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        Validacao.Garantir(filtro);
        Domicilio casa = await _domicilios.ObterAsync(domicilioId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Domicílio não encontrado.");
        return _referencias.TryGetValue(casa.RegiaoClimatica, out PontoGeografico? referencia)
            ? await _pontos.ProximosAsync(referencia, filtro.RaioKm, cancellationToken)
            : [];
    }
}
