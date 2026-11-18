using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>
/// A leitura, ligada à casa por uma chave de sombra. O índice de calor não é gravado: o domínio o recalcula da
/// temperatura e da umidade (é determinístico), e o nível gravado é o da hora da leitura.
/// </summary>
internal sealed class LeituraConfiguration : IEntityTypeConfiguration<Leitura>
{
    /// <summary>O nome da chave de sombra que liga a leitura à casa.</summary>
    internal const string DomicilioId = "DomicilioId";

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Leitura> builder)
    {
        builder.ToTable("leituras");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.DeviceId).HasMaxLength(10);
        builder.Property(l => l.Momento);
        builder.Property(l => l.Temperatura).HasConversion(Conversores.TemperaturaEmCelsius).HasColumnName("temperatura_c");
        builder.Property(l => l.Umidade).HasConversion(Conversores.UmidadeEmPercentual).HasColumnName("umidade_pct");
        builder.Property(l => l.Nivel);
        builder.Ignore(l => l.IndiceDeCalor);
        builder.Property<Guid>(DomicilioId);
        builder.HasOne<Domicilio>().WithMany().HasForeignKey(DomicilioId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(DomicilioId, nameof(Leitura.Momento)).IsDescending(false, true);
    }
}
