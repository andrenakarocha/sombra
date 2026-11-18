using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Respostas;

/// <summary>
/// Os estados de um alerta como o app os mostra (D17). O preventivo, que só informa, aparece como esperando resposta,
/// com a validade de 24 h como prazo; o preventivo que passou da validade aparece como resolvido.
/// </summary>
public enum EstadoDoAlertaNoApp
{
    /// <summary>Esperando o "estou bem" (ou, no preventivo, dentro da validade).</summary>
    AguardandoCheckin = 0,

    /// <summary>O prazo venceu e a rede de cuidado está sendo avisada.</summary>
    Escalonado = 1,

    /// <summary>Um cuidador avisou que vai ligar ou que está indo.</summary>
    Reconhecido = 2,

    /// <summary>Encerrado, pelo "estou bem", por um cuidador ou pelo fim da validade.</summary>
    Resolvido = 3,
}

/// <summary>Os tipos de alerta como o app os mostra (D17): o pedido de ajuda aparece como alerta da Sentinela.</summary>
public enum TipoDoAlertaNoApp
{
    /// <summary>Previsão de calor perigoso nas próximas 24 horas.</summary>
    Preventivo = 0,

    /// <summary>O calor medido na casa (ou um pedido de ajuda) pede resposta.</summary>
    DeSentinela = 1,
}

/// <summary>Um passo da linha do tempo do alerta.</summary>
/// <param name="Momento">Quando aconteceu, em UTC.</param>
/// <param name="Degrau">O degrau do escalonamento naquele momento, de 0 a 3.</param>
/// <param name="Descricao">O que aconteceu, sem dado pessoal.</param>
public sealed record EventoDaLinhaDoTempoResposta(DateTimeOffset Momento, int Degrau, string Descricao);

/// <summary>Um alerta, no contrato do app (D17), com a linha do tempo dos degraus.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="ApelidoDaCasa">Como a família chama a casa.</param>
/// <param name="Tipo">Preventivo ou da Sentinela.</param>
/// <param name="Estado">Onde o alerta está, como o app mostra.</param>
/// <param name="Nivel">O nível de risco atual.</param>
/// <param name="Degrau">O degrau do escalonamento, de 0 a 3.</param>
/// <param name="AbertoEm">Quando foi aberto.</param>
/// <param name="Prazo">O prazo do "estou bem"; no preventivo, o fim da validade.</param>
/// <param name="EncerradoEm">Quando foi encerrado.</param>
/// <param name="MensagemAoMorador">O texto da tela da Sentinela, sem dado pessoal.</param>
/// <param name="LinhaDoTempo">O que aconteceu, na ordem.</param>
public sealed record AlertaResposta(
    Guid Id,
    Guid DomicilioId,
    string ApelidoDaCasa,
    TipoDoAlertaNoApp Tipo,
    EstadoDoAlertaNoApp Estado,
    NivelDeRisco Nivel,
    int Degrau,
    DateTimeOffset AbertoEm,
    DateTimeOffset Prazo,
    DateTimeOffset? EncerradoEm,
    string MensagemAoMorador,
    IReadOnlyList<EventoDaLinhaDoTempoResposta> LinhaDoTempo)
{
    /// <summary>Monta a resposta a partir do alerta.</summary>
    /// <param name="alerta">O alerta.</param>
    /// <param name="apelidoDaCasa">O apelido da casa do alerta.</param>
    /// <returns>A resposta.</returns>
    public static AlertaResposta De(AlertaDeCalor alerta, string apelidoDaCasa)
    {
        ArgumentNullException.ThrowIfNull(alerta);
        return new(
            alerta.Id,
            alerta.DomicilioId,
            apelidoDaCasa,
            alerta is AlertaPreventivo ? TipoDoAlertaNoApp.Preventivo : TipoDoAlertaNoApp.DeSentinela,
            EstadoNoApp(alerta.Estado),
            alerta.Nivel,
            (int)alerta.Degrau,
            alerta.AbertoEm,
            alerta.Prazo ?? (alerta is AlertaPreventivo preventivo ? preventivo.ValidoAte : alerta.AbertoEm),
            alerta.EncerradoEm,
            alerta.MensagemAoMorador,
            [.. alerta.LinhaDoTempo.OrderBy(e => e.Momento).Select(e => new EventoDaLinhaDoTempoResposta(e.Momento, (int)e.Degrau, e.Descricao))]);
    }

    /// <summary>Traduz o estado do domínio para o que o app mostra.</summary>
    /// <param name="estado">O estado no domínio.</param>
    /// <returns>O estado no app.</returns>
    public static EstadoDoAlertaNoApp EstadoNoApp(EstadoDoAlerta estado) => estado switch
    {
        EstadoDoAlerta.Escalonado => EstadoDoAlertaNoApp.Escalonado,
        EstadoDoAlerta.Reconhecido => EstadoDoAlertaNoApp.Reconhecido,
        EstadoDoAlerta.Resolvido or EstadoDoAlerta.Expirado => EstadoDoAlertaNoApp.Resolvido,
        _ => EstadoDoAlertaNoApp.AguardandoCheckin,
    };

    /// <summary>Os estados do domínio que aparecem no app como <paramref name="estado"/>, para filtrar a lista.</summary>
    /// <param name="estado">O estado no app.</param>
    /// <returns>Os estados do domínio.</returns>
    public static IReadOnlyList<EstadoDoAlerta> EstadosDoDominio(EstadoDoAlertaNoApp estado)
        => [.. Enum.GetValues<EstadoDoAlerta>().Where(e => EstadoNoApp(e) == estado)];
}

/// <summary>O resultado de uma leitura: ela, e o alerta que ela abriu ou atualizou.</summary>
/// <param name="Leitura">A leitura classificada.</param>
/// <param name="Alerta">O alerta aberto ou atualizado, se houve.</param>
public sealed record LeituraRegistradaResposta(LeituraResposta Leitura, AlertaResposta? Alerta);

/// <summary>O resultado de um check-in: ele, e o alerta que ele encerrou ou abriu.</summary>
/// <param name="Id">O identificador do check-in.</param>
/// <param name="Momento">Quando foi feito, em UTC.</param>
/// <param name="Origem">De onde veio.</param>
/// <param name="Situacao">O que a pessoa disse.</param>
/// <param name="Alerta">O alerta afetado, se houve.</param>
public sealed record CheckInResposta(Guid Id, DateTimeOffset Momento, OrigemDoCheckIn Origem, SituacaoDoCheckIn Situacao, AlertaResposta? Alerta);
