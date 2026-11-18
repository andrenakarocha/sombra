using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sombra.Infraestrutura.Mensageria;
using Sombra.Infraestrutura.Seguranca;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>O outbox, com índice nos pendentes e no histórico por casa e motivo.</summary>
internal sealed class MensagemDeSaidaConfiguration : IEntityTypeConfiguration<MensagemDeSaida>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<MensagemDeSaida> builder)
    {
        builder.ToTable("outbox");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.CriadaEm);
        builder.Property(m => m.DomicilioId);
        builder.Property(m => m.AlertaId);
        builder.Property(m => m.Destinatario).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Motivo).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Mensagem).HasMaxLength(200);
        builder.Property(m => m.Contatos);
        builder.Property(m => m.CorrelationId).HasMaxLength(64);
        builder.Property(m => m.PublicadaEm);
        builder.Property(m => m.Tentativas);
        builder.Property(m => m.UltimaFalha).HasMaxLength(120);
        builder.HasIndex(m => new { m.PublicadaEm, m.CriadaEm });
        builder.HasIndex(m => new { m.DomicilioId, m.Motivo, m.CriadaEm });
    }
}

/// <summary>As respostas por <c>Idempotency-Key</c>, por usuário e chave, com índice na data para a limpeza.</summary>
internal sealed class RequisicaoIdempotenteConfiguration : IEntityTypeConfiguration<RequisicaoIdempotente>
{
    public void Configure(EntityTypeBuilder<RequisicaoIdempotente> builder)
    {
        builder.ToTable("requisicoes_idempotentes");
        builder.HasKey(r => new { r.Usuario, r.Chave });
        builder.Property(r => r.Chave).HasMaxLength(100);
        builder.Property(r => r.Rota).HasMaxLength(200);
        builder.Property(r => r.Status);
        builder.Property(r => r.Corpo).HasColumnType("text");
        builder.Property(r => r.CriadaEm);
        builder.HasIndex(r => r.CriadaEm);
    }
}

/// <summary>A trilha de segurança, que só cresce.</summary>
internal sealed class EventoDeSegurancaConfiguration : IEntityTypeConfiguration<EventoDeSeguranca>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EventoDeSeguranca> builder)
    {
        builder.ToTable("eventos_de_seguranca");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Momento);
        builder.Property(e => e.Tipo).HasMaxLength(40);
        builder.Property(e => e.Origem).HasMaxLength(64);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.Detalhe).HasMaxLength(200);
        builder.Property(e => e.DomicilioId);
        builder.Property(e => e.Sink).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Categoria).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Severidade).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Regra).HasMaxLength(40);
        builder.Property(e => e.Bloqueado).HasDefaultValue(true);
        builder.Property(e => e.Assinatura).HasMaxLength(64);
        builder.Property(e => e.TraducaoCidadao).HasMaxLength(300);
        builder.Property(e => e.TraducaoGestao).HasMaxLength(300);
        builder.Property(e => e.TraducaoTecnica).HasMaxLength(600);
        builder.Property(e => e.FonteDaTraducao).HasMaxLength(10);
        builder.Property(e => e.TraduzidoEm);
        builder.Ignore(e => e.Traducao);
        builder.HasIndex(e => e.TraduzidoEm);
        builder.HasIndex(e => new { e.Assinatura, e.FonteDaTraducao });
        builder.HasIndex(e => e.Momento);
        builder.HasIndex(e => new { e.DomicilioId, e.Momento });
    }
}
