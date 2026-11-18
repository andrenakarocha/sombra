using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sombra.Dominio.Alertas;
using Sombra.Dominio.Casas;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>
/// A hierarquia de alertas numa tabela só (TPH), com a coluna <c>tipo</c> guardando o mesmo código da API
/// (<c>PREVENTIVO</c>, <c>DE_SENTINELA</c>, <c>PEDIDO_DE_AJUDA</c>). Os eventos de domínio não vão para o banco.
/// </summary>
internal sealed class AlertaConfiguration : IEntityTypeConfiguration<AlertaDeCalor>
{
    private const string Discriminador = "Discriminador";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AlertaDeCalor> builder)
    {
        builder.ToTable("alertas");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.HasDiscriminator<string>(Discriminador)
            .HasValue<AlertaPreventivo>("PREVENTIVO")
            .HasValue<AlertaDeSentinela>("DE_SENTINELA")
            .HasValue<AlertaDePedidoDeAjuda>("PEDIDO_DE_AJUDA");
        builder.Property<string>(Discriminador).HasColumnName("tipo").HasMaxLength(20);

        builder.Property(a => a.DomicilioId);
        builder.Property(a => a.Nivel);
        builder.Property(a => a.AbertoEm);
        builder.Property(a => a.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Degrau);
        builder.Property(a => a.Prazo);
        builder.Property(a => a.Reconhecimento).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.EncerradoEm);
        builder.Ignore(a => a.EventosDeDominio);
        builder.HasOne<Domicilio>().WithMany().HasForeignKey(a => a.DomicilioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(a => new { a.DomicilioId, a.Estado });
        builder.HasIndex(a => new { a.Estado, a.AbertoEm });
    }
}

/// <summary>
/// O alerta de Sentinela guarda quando subiu o último degrau: sem isso, depois de recarregado, o escalonador contaria o
/// intervalo a partir da abertura e pularia degraus.
/// </summary>
internal sealed class AlertaDeSentinelaConfiguration : IEntityTypeConfiguration<AlertaDeSentinela>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AlertaDeSentinela> builder)
        => builder.Property<DateTimeOffset>("_ultimoDegrauEm").HasColumnName("ultimo_degrau_em");
}

/// <summary>O check-in, ligado à casa.</summary>
internal sealed class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("check_ins");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.DomicilioId);
        builder.Property(c => c.Momento);
        builder.Property(c => c.Origem).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Situacao).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Domicilio>().WithMany().HasForeignKey(c => c.DomicilioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(c => new { c.DomicilioId, c.Momento }).IsDescending(false, true);
    }
}
