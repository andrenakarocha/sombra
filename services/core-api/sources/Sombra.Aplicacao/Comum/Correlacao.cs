using System.Threading;

namespace Sombra.Aplicacao.Comum;

/// <summary>
/// O <c>correlationId</c> do trabalho em andamento (§3 de <c>docs/observabilidade.md</c>): o middleware o define por
/// requisição, a ingestão MQTT por mensagem (<c>{deviceId}:{seq}</c>) e os workers por volta. O outbox e os eventos de
/// tempo real o levam adiante.
/// </summary>
public static class Correlacao
{
    private static readonly AsyncLocal<string?> _atual = new();

    /// <summary>Gets or sets a correlação do fluxo assíncrono atual.</summary>
    public static string? Atual
    {
        get => _atual.Value;
        set => _atual.Value = value;
    }
}
