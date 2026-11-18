using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sombra.Infraestrutura.Autenticacao;

namespace Sombra.Infraestrutura.Persistencia.Configuracoes;

/// <summary>As contas de acesso, com e-mail único e as casas do Cuidador num array nativo (<c>uuid[]</c>).</summary>
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasMaxLength(120);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Nome).HasMaxLength(80);
        builder.Property(u => u.SenhaHash).HasMaxLength(200);
        builder.Property(u => u.Papel).HasMaxLength(20);
        builder.PrimitiveCollection(u => u.Casas);
    }
}
