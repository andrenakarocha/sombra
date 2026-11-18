using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Autenticacao;
using Sombra.Infraestrutura.Clima;
using Sombra.Infraestrutura.Mensageria;
using Sombra.Infraestrutura.Mqtt;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;
using Sombra.Infraestrutura.Seguranca;
using Sombra.Infraestrutura.Workers;

namespace Sombra.Infraestrutura;

/// <summary>Liga as portas da aplicação às implementações da infraestrutura.</summary>
public static class ServicosDaInfraestrutura
{
    /// <summary>Registra o banco (um contexto por requisição), os repositórios, as consultas, as contas e o relógio.</summary>
    /// <param name="servicos">O contêiner de serviços.</param>
    /// <param name="conexao">A string de conexão do PostgreSQL do Core.</param>
    /// <returns>O mesmo contêiner.</returns>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection servicos, string conexao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conexao);
        servicos.AddDbContext<SombraDbContext>(opcoes => SombraDbContext.Configurar(opcoes, conexao));
        servicos.AddScoped<IUnitOfWork>(provedor => provedor.GetRequiredService<SombraDbContext>());
        servicos.AddScoped<IDomicilioRepository, DomicilioRepository>();
        servicos.AddScoped<IAlertaRepository, AlertaRepository>();
        servicos.AddScoped<ILeituraRepository, LeituraRepository>();
        servicos.AddScoped<ICheckInRepository, CheckInRepository>();
        servicos.AddScoped<IConsultasDoPainel, ConsultasDoPainel>();
        servicos.AddScoped<ContasDeAcesso>();
        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton<RelogioDoSistema>();
        servicos.AddSingleton<IRelogio>(provedor => provedor.GetRequiredService<RelogioDoSistema>());
        servicos.AddSingleton<DespachanteDeFatos>();
        servicos.TryAddSingleton<ITempoReal, TempoRealDesligado>();
        servicos.AddScoped<NotificadorComOutbox>();
        servicos.AddScoped<INotificador>(provedor => provedor.GetRequiredService<NotificadorComOutbox>());
        servicos.AddScoped<IHistoricoDeAvisos>(provedor => provedor.GetRequiredService<NotificadorComOutbox>());
        servicos.AddSingleton<IClimaGateway, ClimaSimulado>();
        servicos.AddSingleton<ICodigosDeVinculo, CodigosDeVinculo>();
        servicos.AddScoped<IRegistroDeIdempotencia, RegistroDeIdempotencia>();
        return servicos;
    }

    /// <summary>
    /// Registra a conversa com as Sentinelas e os workers: a conexão MQTT, a ingestão, os canais de entrega, o publicador do
    /// outbox, o escalonador (com a rotina) e o monitor de previsão.
    /// </summary>
    /// <param name="servicos">O contêiner de serviços.</param>
    /// <returns>O mesmo contêiner.</returns>
    public static IServiceCollection AdicionarMensageria(this IServiceCollection servicos)
    {
        servicos.AddSingleton<ConexaoMqtt>();
        servicos.AddScoped<ProcessadorDaSentinela>();
        servicos.AddScoped<ICanalDeNotificacao, CanalSentinela>();
        servicos.AddScoped<ICanalDeNotificacao, CanalApp>();
        servicos.AddScoped<ICanalDeNotificacao, CanalSmsSimulado>();
        servicos.AddHostedService<IngestaoMqttWorker>();
        servicos.AddHostedService<PublicadorDeComandosWorker>();
        servicos.AddHostedService<EscalonadorDeAlertasWorker>();
        servicos.AddHostedService<MonitorDePrevisaoWorker>();
        return servicos;
    }
}
