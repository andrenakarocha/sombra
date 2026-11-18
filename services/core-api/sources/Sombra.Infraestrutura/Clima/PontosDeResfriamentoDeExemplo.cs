using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sombra.Aplicacao.Portas;
using Sombra.Aplicacao.Respostas;
using Sombra.Dominio.Geo;

namespace Sombra.Infraestrutura.Clima;

/// <summary>
/// Os pontos de resfriamento enquanto o Clima não atende (F3.9): uma lista de exemplo, rotulada como exemplo, perto da
/// estação A701. Nada aqui é um lugar real; a distância é calculada de verdade, pela fórmula de haversine.
/// </summary>
public sealed class PontosDeResfriamentoDeExemplo : IPontosDeResfriamento
{
    /// <summary>A fonte que aparece em todo ponto desta lista.</summary>
    public const string Fonte = "dados de exemplo do Sombra (a lista real vem do Clima na F3.9)";

    private static readonly (string Id, string Nome, string Tipo, string Abre, string Fecha, bool Acessivel, double Lat, double Lon)[] _pontos =
    [
        ("9a1f2c3d-0001-4b5c-8d6e-7f8091a2b3c4", "Biblioteca do bairro (exemplo)", "BIBLIOTECA", "09:00", "18:00", true, -23.5030, -46.6230),
        ("9a1f2c3d-0002-4b5c-8d6e-7f8091a2b3c4", "Centro de convivência do idoso (exemplo)", "CENTRO_DE_CONVIVENCIA", "08:00", "17:00", true, -23.4890, -46.6110),
        ("9a1f2c3d-0003-4b5c-8d6e-7f8091a2b3c4", "UBS do bairro (exemplo)", "UBS", "07:00", "19:00", true, -23.5120, -46.6290),
        ("9a1f2c3d-0004-4b5c-8d6e-7f8091a2b3c4", "Shopping climatizado (exemplo)", "SHOPPING", "10:00", "22:00", true, -23.4790, -46.6050),
        ("9a1f2c3d-0005-4b5c-8d6e-7f8091a2b3c4", "Salão paroquial com ar-condicionado (exemplo)", "ESPACO_COMUNITARIO", "08:00", "12:00", false, -23.4600, -46.6400),
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<PontoDeResfriamentoResposta>> ProximosAsync(PontoGeografico origem, double raioKm, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(origem);
        IReadOnlyList<PontoDeResfriamentoResposta> proximos = [.. _pontos
            .Select(p => (Ponto: p, Distancia: origem.DistanciaKmAte(PontoGeografico.Criar(p.Lat, p.Lon))))
            .Where(p => p.Distancia <= raioKm)
            .OrderBy(p => p.Distancia)
            .Select(p => new PontoDeResfriamentoResposta(
                p.Ponto.Id,
                p.Ponto.Nome,
                p.Ponto.Tipo,
                Math.Round(p.Distancia, 1, MidpointRounding.AwayFromZero),
                new HorarioDeFuncionamentoResposta(p.Ponto.Abre, p.Ponto.Fecha),
                p.Ponto.Acessivel,
                Fonte,
                p.Ponto.Lat,
                p.Ponto.Lon))];
        return Task.FromResult(proximos);
    }
}
