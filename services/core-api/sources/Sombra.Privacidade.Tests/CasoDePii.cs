using System.Collections.Generic;

namespace Sombra.Privacidade.Tests;

/// <summary>Um caso da tabela do motor de PII.</summary>
/// <param name="Nome">O que o caso prova.</param>
/// <param name="Tipo">Positivo, Negativo ou Difícil.</param>
/// <param name="Chave">O nome do campo, quando o valor vem de um JSON.</param>
/// <param name="Texto">O valor analisado (só dados sintéticos).</param>
/// <param name="Esperado">As regras que precisam disparar, e só elas.</param>
public sealed record CasoDePii(string Nome, string Tipo, string? Chave, string Texto, IReadOnlyList<string> Esperado)
{
    /// <summary>O nome do caso no relatório do MSTest; nunca mostra o texto, que tem PII sintética.</summary>
    /// <returns>Tipo e nome.</returns>
    public override string ToString() => $"{Tipo}: {Nome}";
}
