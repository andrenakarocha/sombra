using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sombra.Dominio.Casas;
using Sombra.Dominio.Sentinelas;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>A casa: endereço e rotina como tipos complexos (value objects sem identidade), moradores e Sentinelas como filhos.</summary>
internal sealed class DomicilioConfiguration : IEntityTypeConfiguration<Domicilio>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Domicilio> builder)
    {
        builder.ToTable("domicilios");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Apelido).HasMaxLength(60);
        builder.Property(d => d.RegiaoClimatica).HasMaxLength(33);
        builder.Property(d => d.Monitorado);

        builder.ComplexProperty(d => d.Endereco, endereco =>
        {
            endereco.Property(e => e.Logradouro).HasMaxLength(120);
            endereco.Property(e => e.Numero).HasMaxLength(10);
            endereco.Property(e => e.Bairro).HasMaxLength(60);
            endereco.Property(e => e.Cidade).HasMaxLength(60);
            endereco.Property(e => e.Uf).HasMaxLength(2);
            endereco.Property(e => e.Cep).HasMaxLength(8);
        });

        builder.ComplexProperty(d => d.Rotina, rotina =>
        {
            rotina.Property(r => r.HorarioDoCheckInDiario);
            rotina.Property(r => r.LembreteDeAguaAtivo);
        });

        builder.Property(d => d.RedeDeCuidado)
            .HasConversion(Conversores.RedeEmJson, Conversores.RedePorConteudo)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");

        builder.HasMany(d => d.Moradores).WithOne().HasForeignKey("DomicilioId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Sentinelas).WithOne().HasForeignKey("DomicilioId").IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.Moradores).AutoInclude();
        builder.Navigation(d => d.Sentinelas).AutoInclude();
    }
}

/// <summary>O morador. CPF, Cartão SUS e saúde passam por conversores (a F2.3 os cifra).</summary>
internal sealed class MoradorConfiguration : IEntityTypeConfiguration<Morador>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Morador> builder)
    {
        builder.ToTable("moradores");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Nome).HasMaxLength(80);
        builder.Property(m => m.DataDeNascimento);
        builder.Property(m => m.Cpf).HasConversion(Conversores.CpfEmTexto).HasMaxLength(11);
        builder.Property(m => m.CartaoSus).HasConversion(Conversores.CartaoSusEmTexto).HasMaxLength(15);
        builder.Property(m => m.PerfilDeSaude).HasConversion(Conversores.PerfilEmCodigos).HasMaxLength(200);
        builder.Property(m => m.Telefone).HasConversion(Conversores.TelefoneEmTexto).HasMaxLength(11);
        builder.HasIndex("DomicilioId", nameof(Morador.Cpf)).IsUnique();
    }
}

/// <summary>A Sentinela, identificada pelo <c>deviceId</c> do contrato MQTT (único no sistema todo).</summary>
internal sealed class SentinelaConfiguration : IEntityTypeConfiguration<Sentinela>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Sentinela> builder)
    {
        builder.ToTable("sentinelas");
        builder.HasKey(s => s.DeviceId);
        builder.Property(s => s.DeviceId).HasMaxLength(10).ValueGeneratedNever();
        builder.Property(s => s.UltimoContato);
        builder.Property(s => s.SegredoHex).HasMaxLength(64);
        builder.Property(s => s.UltimoSeqRecebido);
        builder.Property(s => s.UltimoSeqEnviado);
    }
}
