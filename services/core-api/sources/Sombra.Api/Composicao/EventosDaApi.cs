using System;
using Microsoft.Extensions.Logging;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Taint;

namespace Sombra.Api.Composicao;

/// <summary>Os eventos de log da API (catálogo em <c>docs/observabilidade.md</c> §4).</summary>
internal static partial class EventosDaApi
{
    /// <summary>Uma requisição terminou: método, rota-modelo, status e duração, nunca o corpo.</summary>
    [LoggerMessage(EventId = 1001, EventName = "requisicao.concluida", Message = "{metodo} {rota} respondeu {status} em {duracaoMs} ms")]
    internal static partial void RequisicaoConcluida(this ILogger logger, LogLevel nivel, string metodo, string rota, int status, double duracaoMs);

    /// <summary>Uma exceção inesperada quebrou a requisição (o stack trace fica só no log).</summary>
    [LoggerMessage(EventId = 1002, EventName = "requisicao.falhou", Level = LogLevel.Error, Message = "Exceção não tratada em {metodo} {rota}")]
    internal static partial void RequisicaoFalhou(this ILogger logger, Exception excecao, string metodo, string rota);

    /// <summary>O Core subiu, com a configuração que importa e sem segredos.</summary>
    [LoggerMessage(EventId = 1003, EventName = "servico.iniciado", Level = LogLevel.Information,
        Message = "Core API iniciada: versão {versao}, ambiente {ambiente}, migrar ao iniciar {migrarAoIniciar}, contas da demo {contasDaDemo}, limite {limitePorMinuto}/min, login {limiteDeLoginPorMinuto}/min, emissor do JWT {emissorDoJwt}")]
    internal static partial void ServicoIniciado(this ILogger logger, string versao, string ambiente, bool migrarAoIniciar, bool contasDaDemo, int limitePorMinuto, int limiteDeLoginPorMinuto, string emissorDoJwt);

    /// <summary>
    /// Um dado pessoal entrou numa requisição (spec 07 §1): família, campo, fonte, regra e o que foi feito, nunca o valor.
    /// O esperado (o CPF no campo cpf) é Debug; o fora do lugar (mascarado ou recusado) é Warning.
    /// </summary>
    [LoggerMessage(EventId = 1101, EventName = "privacidade.pii_detectada", Message = "PII {categoria} em {campo} ({fonte}, regra {regra}, pela {origem}): {desfecho}")]
    internal static partial void PiiDetectada(this ILogger logger, LogLevel nivel, CategoriaDePii categoria, string campo, FonteDePii fonte, string regra, OrigemDaDeteccao origem, DesfechoDaDeteccao desfecho);

    /// <summary>O Core está parando.</summary>
    [LoggerMessage(EventId = 1004, EventName = "servico.encerrado", Level = LogLevel.Information, Message = "Core API encerrada")]
    internal static partial void ServicoEncerrado(this ILogger logger);
}
