using System;

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
}
