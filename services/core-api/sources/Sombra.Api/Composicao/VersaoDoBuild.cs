using System;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Sombra.Api.Controllers;

namespace Sombra.Api.Composicao;

/// <summary>
/// A versão em execução, a mesma no <c>/version</c> e no campo <c>version</c> de cada linha de log. O commit vem de
/// <c>Versao__Commit</c> (a imagem Docker recebe no build) ou, sem ele, do SourceLink do próprio build.
/// </summary>
public static class VersaoDoBuild
{
    /// <summary>Lê a versão.</summary>
    /// <param name="configuracao">A configuração (<c>Versao:Commit</c>, <c>Versao:ConstruidoEm</c>).</param>
    /// <returns>Versão do assembly, commit e data do build.</returns>
    public static VersaoResposta De(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        string informacional = typeof(VersaoDoBuild).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        string[] partes = informacional.Split('+', 2);
        string commit = configuracao["Versao:Commit"] is { Length: > 0 } configurado && configurado != "dev"
            ? configurado
            : partes.Length == 2 ? partes[1][..Math.Min(7, partes[1].Length)] : "dev";
        return new VersaoResposta(partes[0], commit, configuracao["Versao:ConstruidoEm"] ?? string.Empty);
    }
}
