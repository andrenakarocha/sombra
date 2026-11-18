using System;
using Microsoft.Extensions.Logging;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Calor;
using Sombra.Dominio.Casas;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// Os fatos de negócio no log, registrados depois de gravados (catálogo em <c>docs/observabilidade.md</c> §4). Só IDs,
/// níveis e códigos: nunca nome, documento, telefone ou endereço.
/// </summary>
internal static partial class EventosDaPersistencia
{
    [LoggerMessage(EventId = 3001, EventName = "leitura.registrada", Level = LogLevel.Information, Message = "Leitura da Sentinela {deviceId} na casa {domicilioId}: nível {nivel}")]
    internal static partial void LeituraRegistrada(this ILogger logger, string deviceId, Guid domicilioId, NivelDeRisco nivel);

    [LoggerMessage(EventId = 3002, EventName = "checkin.registrado", Level = LogLevel.Information, Message = "Check-in {situacao} ({origem}) na casa {domicilioId}")]
    internal static partial void CheckInRegistrado(this ILogger logger, SituacaoDoCheckIn situacao, OrigemDoCheckIn origem, Guid domicilioId);

    [LoggerMessage(EventId = 3003, EventName = "pedido_de_ajuda.recebido", Level = LogLevel.Warning, Message = "Pedido de ajuda na casa {domicilioId} ({origem})")]
    internal static partial void PedidoDeAjudaRecebido(this ILogger logger, Guid domicilioId, OrigemDoCheckIn origem);

    [LoggerMessage(EventId = 3010, EventName = "alerta.aberto", Level = LogLevel.Information, Message = "Alerta {alertaId} ({tipo}) aberto na casa {domicilioId} com nível {nivel}")]
    internal static partial void AlertaAberto(this ILogger logger, Guid alertaId, string tipo, Guid domicilioId, NivelDeRisco nivel);

    [LoggerMessage(EventId = 3011, EventName = "alerta.agravado", Level = LogLevel.Information, Message = "Alerta {alertaId} da casa {domicilioId} subiu para o nível {nivel}")]
    internal static partial void AlertaAgravado(this ILogger logger, Guid alertaId, Guid domicilioId, NivelDeRisco nivel);

    [LoggerMessage(EventId = 3012, EventName = "alerta.escalonado", Level = LogLevel.Information, Message = "Alerta {alertaId} da casa {domicilioId} escalonado para o degrau {degrau}")]
    internal static partial void AlertaEscalonado(this ILogger logger, Guid alertaId, Guid domicilioId, int degrau);

    [LoggerMessage(EventId = 3013, EventName = "alerta.reconhecido", Level = LogLevel.Information, Message = "Alerta {alertaId} da casa {domicilioId} respondido pelo cuidador: {acao}")]
    internal static partial void AlertaReconhecido(this ILogger logger, Guid alertaId, Guid domicilioId, AcaoDoCuidador acao);

    [LoggerMessage(EventId = 3014, EventName = "alerta.resolvido", Level = LogLevel.Information, Message = "Alerta {alertaId} da casa {domicilioId} resolvido em {minutos} min")]
    internal static partial void AlertaResolvido(this ILogger logger, Guid alertaId, Guid domicilioId, double minutos);

    [LoggerMessage(EventId = 3015, EventName = "alerta.expirado", Level = LogLevel.Information, Message = "Alerta {alertaId} da casa {domicilioId} expirou")]
    internal static partial void AlertaExpirado(this ILogger logger, Guid alertaId, Guid domicilioId);
}
