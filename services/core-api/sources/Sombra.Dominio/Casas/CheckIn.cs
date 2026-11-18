using System;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Casas;

/// <summary>De onde veio o check-in.</summary>
public enum OrigemDoCheckIn
{
    /// <summary>Botão físico da Sentinela.</summary>
    BotaoSentinela = 0,

    /// <summary>Modo Idoso do app.</summary>
    App = 1,

    /// <summary>Um cuidador que foi ver ou falou com a pessoa.</summary>
    Cuidador = 2,
}

/// <summary>O que a pessoa disse no check-in.</summary>
public enum SituacaoDoCheckIn
{
    /// <summary>"Estou bem".</summary>
    EstouBem = 0,

    /// <summary>"Preciso de ajuda" (abre um pedido de ajuda, regra R7).</summary>
    PrecisoDeAjuda = 1,
}

/// <summary>Um "estou bem" ou "preciso de ajuda" registrado para uma casa.</summary>
public sealed class CheckIn
{
    /// <summary>Até quanto tempo depois do toque um check-in que esperou a rede ainda vale (regra R14).</summary>
    public static readonly TimeSpan JanelaOffline = TimeSpan.FromHours(24);

    /// <summary>Quanto o relógio do celular pode estar adiantado sem o check-in ser recusado (regra R14).</summary>
    public static readonly TimeSpan ToleranciaDoRelogio = TimeSpan.FromMinutes(2);

    private CheckIn(Guid domicilioId, DateTimeOffset momento, OrigemDoCheckIn origem, SituacaoDoCheckIn situacao)
    {
        Id = Guid.CreateVersion7();
        DomicilioId = domicilioId;
        Momento = momento.ToUniversalTime();
        Origem = origem;
        Situacao = situacao;
    }

    /// <summary>Gets o identificador.</summary>
    public Guid Id { get; }

    /// <summary>Gets a casa.</summary>
    public Guid DomicilioId { get; }

    /// <summary>Gets quando foi feito, em UTC.</summary>
    public DateTimeOffset Momento { get; }

    /// <summary>Gets de onde veio.</summary>
    public OrigemDoCheckIn Origem { get; }

    /// <summary>Gets o que a pessoa disse.</summary>
    public SituacaoDoCheckIn Situacao { get; }

    /// <summary>Registra um check-in.</summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="momento">Quando foi feito.</param>
    /// <param name="origem">De onde veio.</param>
    /// <param name="situacao">O que a pessoa disse.</param>
    /// <returns>O check-in.</returns>
    public static CheckIn Registrar(Guid domicilioId, DateTimeOffset momento, OrigemDoCheckIn origem, SituacaoDoCheckIn situacao)
        => new(domicilioId, momento, origem, situacao);

    /// <summary>
    /// Registra um check-in no horário em que a pessoa apertou, que pode ser antes de a rede voltar (fila offline do app).
    /// Regra R14: nunca no futuro (com a tolerância do relógio do celular) e até 24 horas atrás; sem horário, vale o agora.
    /// </summary>
    /// <param name="domicilioId">A casa.</param>
    /// <param name="tocadoEm">Quando a pessoa apertou, se o app mandou.</param>
    /// <param name="agora">O instante em que o Core recebeu.</param>
    /// <param name="origem">De onde veio.</param>
    /// <param name="situacao">O que a pessoa disse.</param>
    /// <returns>O check-in.</returns>
    /// <exception cref="RegraVioladaException">Horário no futuro ou de mais de 24 horas atrás (R14).</exception>
    public static CheckIn RegistrarTocadoEm(Guid domicilioId, DateTimeOffset? tocadoEm, DateTimeOffset agora, OrigemDoCheckIn origem, SituacaoDoCheckIn situacao)
    {
        DateTimeOffset momento = tocadoEm ?? agora;
        if (momento > agora + ToleranciaDoRelogio)
        {
            throw new RegraVioladaException("R14", "O horário do check-in está no futuro. Confira o relógio do celular.");
        }

        if (momento < agora - JanelaOffline)
        {
            throw new RegraVioladaException("R14", "Este check-in esperou mais de 24 horas e não vale mais.");
        }

        return new(domicilioId, momento > agora ? agora : momento, origem, situacao);
    }
}
