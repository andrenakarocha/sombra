namespace Sombra.Dominio.Alertas;

/// <summary>Os estados de um alerta (regra R5).</summary>
public enum EstadoDoAlerta
{
    /// <summary>Alerta preventivo: só informa, não pede check-in.</summary>
    Informativo = 0,

    /// <summary>Esperando o "estou bem" dentro do prazo.</summary>
    AguardandoCheckIn = 1,

    /// <summary>O prazo venceu e a rede de cuidado está sendo avisada.</summary>
    Escalonado = 2,

    /// <summary>Um cuidador avisou que vai ligar ou que está indo; o escalonamento para.</summary>
    Reconhecido = 3,

    /// <summary>Encerrado com o "estou bem" ou com um cuidador confirmando que a pessoa está bem.</summary>
    Resolvido = 4,

    /// <summary>Encerrado pelo tempo (alerta preventivo que passou da validade).</summary>
    Expirado = 5,
}

/// <summary>Os degraus do escalonamento (D13), do mais leve ao mais forte.</summary>
public enum DegrauDeEscalonamento
{
    /// <summary>Degrau 0: a Sentinela lembra a pessoa e explica o próximo passo.</summary>
    Sentinela = 0,

    /// <summary>Degrau 1: lembrete leve ao primeiro familiar.</summary>
    LembreteLeve = 1,

    /// <summary>Degrau 2: "pode dar uma olhada?" para quem está perto (vizinho).</summary>
    QuemEstaPerto = 2,

    /// <summary>Degrau 3: alerta para toda a rede de cuidado.</summary>
    AlertaParaARede = 3,
}

/// <summary>O que um cuidador responde a um alerta pelo app.</summary>
public enum AcaoDoCuidador
{
    /// <summary>Vai ligar para a pessoa.</summary>
    VouLigar = 0,

    /// <summary>Está indo até a casa.</summary>
    EstouIndo = 1,

    /// <summary>Confirmou que a pessoa está bem.</summary>
    ElaEstaBem = 2,
}
