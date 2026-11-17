using System;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Aplicacao.Respostas;

/// <summary>Um alerta e onde ele está na máquina de estados.</summary>
/// <param name="Id">O identificador.</param>
/// <param name="DomicilioId">A casa.</param>
/// <param name="Tipo">O tipo: <c>PREVENTIVO</c>, <c>DE_SENTINELA</c> ou <c>PEDIDO_DE_AJUDA</c>.</param>
/// <param name="Estado">O estado atual.</param>
/// <param name="Nivel">O nível de risco atual.</param>
/// <param name="Degrau">O degrau do escalonamento, de 0 a 3.</param>
/// <param name="AbertoEm">Quando foi aberto.</param>
/// <param name="Prazo">O prazo do "estou bem", quando o tipo pede.</param>
/// <param name="EncerradoEm">Quando foi encerrado.</param>
/// <param name="MensagemAoMorador">O texto da tela da Sentinela, sem dado pessoal.</param>
public sealed record AlertaResposta(
    Guid Id,
    Guid DomicilioId,
    string Tipo,
    EstadoDoAlerta Estado,
    NivelDeRisco Nivel,
    int Degrau,
    DateTimeOffset AbertoEm,
    DateTimeOffset? Prazo,
    DateTimeOffset? EncerradoEm,
    string MensagemAoMorador)
{
    /// <summary>Monta a resposta a partir do alerta.</summary>
    /// <param name="alerta">O alerta.</param>
    /// <returns>A resposta.</returns>
    public static AlertaResposta De(AlertaDeCalor alerta)
    {
        ArgumentNullException.ThrowIfNull(alerta);
        return new(
            alerta.Id,
            alerta.DomicilioId,
            alerta.Tipo,
            alerta.Estado,
            alerta.Nivel,
            (int)alerta.Degrau,
            alerta.AbertoEm,
            alerta.Prazo,
            alerta.EncerradoEm,
            alerta.MensagemAoMorador);
    }
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
