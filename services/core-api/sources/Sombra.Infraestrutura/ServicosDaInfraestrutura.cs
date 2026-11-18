using System;
using Microsoft.Extensions.DependencyInjection;
using Sombra.Aplicacao.Portas;
using Sombra.Infraestrutura.Autenticacao;
using Sombra.Infraestrutura.Notificacoes;
using Sombra.Infraestrutura.Persistencia;
using Sombra.Infraestrutura.Persistencia.Repositorios;

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
        servicos.AddSingleton<IRelogio, RelogioDoSistema>();
        servicos.AddSingleton<INotificador, NotificadorDeLog>();
        return servicos;
    }
}
