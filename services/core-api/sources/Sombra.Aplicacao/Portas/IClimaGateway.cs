using System.Threading;
using System.Threading.Tasks;
using Sombra.Dominio.Calor;

namespace Sombra.Aplicacao.Portas;

/// <summary>O risco de calor de uma região, segundo o Clima Service.</summary>
/// <param name="NivelAgora">O nível observado agora na estação.</param>
/// <param name="NivelPrevisto">O nível previsto para as próximas 24 h.</param>
/// <param name="ProbabilidadePerigo">A probabilidade de perigo em 24 h, de 0 a 1.</param>
/// <param name="Fonte">De onde veio a previsão: <c>onnx</c> (modelo de ML) ou <c>regra</c> (fallback).</param>
public sealed record RiscoDaRegiao(NivelDeRisco NivelAgora, NivelDeRisco NivelPrevisto, double ProbabilidadePerigo, string Fonte);

/// <summary>A conversa com o Clima Service (contrato <c>docs/contratos/clima-service.md</c>).</summary>
public interface IClimaGateway
{
    /// <summary>Consulta o risco de calor de uma região. Não lança: falha de rede, timeout ou circuito aberto viram nulo.</summary>
    /// <param name="regiaoClimatica">O código da região, ex.: <c>SP-MIRANTE</c>.</param>
    /// <param name="cancellationToken">Cancela a consulta.</param>
    /// <returns>O risco, ou nulo quando o Clima não respondeu ou não conhece a região.</returns>
    Task<RiscoDaRegiao?> ObterRiscoAsync(string regiaoClimatica, CancellationToken cancellationToken);
}
