using System;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sombra.Privacidade.Repouso;

namespace Sombra.Infraestrutura.Persistencia;

/// <summary>
/// Cria o contexto para o <c>dotnet ef</c> (migrações e <c>database update</c>). A conexão vem de <c>SOMBRA_CORE_DB</c>;
/// sem ela, vale um endereço local que só serve para gerar migrações, que não conectam. As migrações não cifram nada (o
/// legado é cifrado pelo Core ao subir, em <see cref="SombraDbContext.CifrarLegadoAsync"/>), então a cifra aqui usa chaves
/// descartáveis.
/// </summary>
public sealed class SombraDbContextFactory : IDesignTimeDbContextFactory<SombraDbContext>
{
    /// <inheritdoc/>
    public SombraDbContext CreateDbContext(string[] args)
    {
        string conexao = Environment.GetEnvironmentVariable("SOMBRA_CORE_DB") ?? "Host=localhost;Database=sombra_core;Username=sombra_core";
        DbContextOptionsBuilder<SombraDbContext> builder = new();
        SombraDbContext.Configurar(builder, conexao);
        return new SombraDbContext(builder.Options, new CifraDeCampos(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32)));
    }
}
