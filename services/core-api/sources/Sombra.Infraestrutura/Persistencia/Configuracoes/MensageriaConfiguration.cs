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
        builder.HasIndex(e => e.Momento);
    }
}
