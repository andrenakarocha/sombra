using System;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Alertas;

/// <summary>
/// Nasce da previsão de perigo nas próximas 24 h, antes de a casa esquentar. Só informa: não pede check-in nem escalona,
/// e expira quando a janela da previsão passa.
/// </summary>
public sealed class AlertaPreventivo : AlertaDeCalor
{
    /// <summary>Por quanto tempo a previsão vale.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    private AlertaPreventivo(Guid domicilioId, NivelDeRisco nivel, DateTimeOffset abertoEm)
        : base(domicilioId, nivel, abertoEm, EstadoDoAlerta.Informativo)
    {
    }

    /// <inheritdoc/>
    public override string Tipo => "PREVENTIVO";

    /// <inheritdoc/>
    public override string MensagemAoMorador => "Amanhã: calor perigoso. Encha sua garrafa de água.";

    /// <inheritdoc/>
    protected override string DescricaoDaAbertura => "Previsão de calor perigoso nas próximas 24 horas.";

    /// <summary>Gets até quando a previsão vale.</summary>
    public DateTimeOffset ValidoAte => AbertoEm + Validade;

    /// <summary>Abre o alerta a partir da previsão do Clima.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="nivelPrevisto">O nível previsto para as próximas 24 h.</param>
    /// <param name="momento">Quando a previsão chegou.</param>
    /// <returns>O alerta.</returns>
    /// <exception cref="RegraVioladaException">Quando a previsão não chega a perigo.</exception>
    public static AlertaPreventivo Abrir(Guid domicilioId, NivelDeRisco nivelPrevisto, DateTimeOffset momento)
    {
        if (nivelPrevisto < NivelDeRisco.Perigo)
        {
            throw new RegraVioladaException("R5", "O alerta preventivo é só para previsão de perigo.");
        }

        AlertaPreventivo alerta = new(domicilioId, nivelPrevisto, momento);
        alerta.RegistrarAbertura();
        return alerta;
    }

    /// <summary>O preventivo não pede "estou bem": quem pede é o alerta de Sentinela.</summary>
    /// <param name="momento">Quando a pessoa apertou.</param>
    /// <param name="origem">De onde veio o "estou bem".</param>
    /// <exception cref="RegraVioladaException">Sempre (R5).</exception>
    public override void ConfirmarEstouBem(DateTimeOffset momento, OrigemDoCheckIn origem)
        => throw new RegraVioladaException("R5", "O alerta preventivo não pede check-in.");

    /// <summary>Encerra o alerta quando a janela da previsão passou.</summary>
    /// <param name="agora">O instante de referência.</param>
    /// <returns><c>true</c> quando expirou agora.</returns>
    public bool ExpirarSeVencido(DateTimeOffset agora)
    {
        if (!EstaAberto || agora < ValidoAte)
        {
            return false;
        }

        Anotar(agora, "A previsão de 24 horas terminou.");
        Encerrar(EstadoDoAlerta.Expirado, agora);
        return true;
    }
}
