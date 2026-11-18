using System;
using Sombra.Dominio.Calor;

namespace Sombra.Dominio.Alertas;

/// <summary>
/// Nasce do botão vermelho (pressão longa) ou do "preciso de ajuda" no app. Regra R7: vai direto ao degrau 3, a rede toda,
/// em qualquer nível de calor.
/// </summary>
public sealed class AlertaDePedidoDeAjuda : AlertaDeCalor
{
    private AlertaDePedidoDeAjuda(Guid domicilioId, NivelDeRisco nivel, DateTimeOffset abertoEm)
        : base(domicilioId, nivel, abertoEm, EstadoDoAlerta.AguardandoCheckIn)
    {
    }

    /// <inheritdoc/>
    public override string Tipo => "PEDIDO_DE_AJUDA";

    /// <inheritdoc/>
    public override string MensagemAoMorador => "Pedido recebido. Sua família foi avisada.";

    /// <inheritdoc/>
    protected override string DescricaoDaAbertura => "Pedido de ajuda pela Sentinela ou pelo app.";

    /// <summary>Abre o pedido de ajuda e avisa a rede inteira na hora.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="nivelAtual">O nível de calor no momento, só para contexto.</param>
    /// <param name="momento">Quando a pessoa pediu ajuda.</param>
    /// <returns>O alerta, já no degrau 3.</returns>
    public static AlertaDePedidoDeAjuda Abrir(Guid domicilioId, NivelDeRisco nivelAtual, DateTimeOffset momento)
    {
        AlertaDePedidoDeAjuda alerta = new(domicilioId, nivelAtual, momento);
        alerta.RegistrarAbertura();
        alerta.SubirPara(DegrauDeEscalonamento.AlertaParaARede, momento, "Toda a rede de cuidado foi avisada.");
        return alerta;
    }
}
