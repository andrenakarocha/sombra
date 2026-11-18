using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Privacidade.Deteccao;

namespace Sombra.Privacidade.Tests;

/// <summary>
/// O relatório de acertos do motor (🔎 da F2.1): roda a tabela, conta acertos por regra e exige zero falso negativo.
/// Com <c>SOMBRA_RELATORIO_PII</c> definido (o <c>make pii-relatorio</c>), grava o relatório em Markdown nesse caminho.
/// O relatório só tem nomes de casos e contagens, nunca o texto analisado.
/// </summary>
[TestClass]
public sealed class RelatorioDeAcertosTests
{
    /// <summary>Zero falso negativo na tabela, e o relatório gravado quando pedido.</summary>
    [TestMethod]
    public void RelatorioDeAcertos()
    {
        MotorDePii motor = MotorDePii.Padrao;
        List<(CasoDePii Caso, string[] Obtido)> resultados = [.. CasosDePii.Todos.Select(c =>
            (c, motor.Analisar(c.Texto, c.Chave).Select(o => o.RegraId).Distinct().Order(StringComparer.Ordinal).ToArray()))];

        var porRegra = motor.Regras.Select(r => new
        {
            r.Id,
            Esperados = resultados.Count(x => x.Caso.Esperado.Contains(r.Id)),
            Acertos = resultados.Count(x => x.Caso.Esperado.Contains(r.Id) && x.Obtido.Contains(r.Id)),
            FalsosPositivos = resultados.Count(x => !x.Caso.Esperado.Contains(r.Id) && x.Obtido.Contains(r.Id)),
        }).ToList();

        string? caminho = Environment.GetEnvironmentVariable("SOMBRA_RELATORIO_PII");
        if (!string.IsNullOrWhiteSpace(caminho))
        {
            File.WriteAllText(caminho, Montar(resultados, porRegra.Select(p => (p.Id, p.Esperados, p.Acertos, p.FalsosPositivos)).ToList()));
        }

        int falsosNegativos = porRegra.Sum(p => p.Esperados - p.Acertos);
        Assert.AreEqual(0, falsosNegativos, "falso negativo na tabela: " + string.Join(", ", porRegra.Where(p => p.Esperados > p.Acertos).Select(p => p.Id)));
    }

    private static string Montar(List<(CasoDePii Caso, string[] Obtido)> resultados, List<(string Id, int Esperados, int Acertos, int FalsosPositivos)> porRegra)
    {
        CultureInfo c = CultureInfo.InvariantCulture;
        int corretos = resultados.Count(x => x.Obtido.SequenceEqual(x.Caso.Esperado.Order(StringComparer.Ordinal)));
        StringBuilder md = new();
        md.AppendLine("# Relatório de acertos do motor de PII")
            .AppendLine()
            .AppendLine("> Gerado por `make pii-relatorio` (teste `RelatorioDeAcertos` em `Sombra.Privacidade.Tests`). Regras: `pii-regras.json`.")
            .AppendLine("> Os casos usam só dados sintéticos e o relatório não reproduz nenhum texto analisado.")
            .AppendLine()
            .AppendLine(c, $"**{corretos} de {resultados.Count} casos corretos** · falsos negativos: **{porRegra.Sum(p => p.Esperados - p.Acertos)}** · falsos positivos: **{porRegra.Sum(p => p.FalsosPositivos)}**")
            .AppendLine()
            .AppendLine("## Por regra")
            .AppendLine()
            .AppendLine("| Regra | Casos com o dado | Detectados | Falsos negativos | Falsos positivos | Recall |")
            .AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach ((string id, int esperados, int acertos, int falsosPositivos) in porRegra)
        {
            string recall = esperados == 0 ? "–" : ((double)acertos / esperados).ToString("P0", c);
            md.AppendLine(c, $"| `{id}` | {esperados} | {acertos} | {esperados - acertos} | {falsosPositivos} | {recall} |");
        }

        md.AppendLine()
            .AppendLine("## Por tipo de caso")
            .AppendLine()
            .AppendLine("| Tipo | Casos | Corretos |")
            .AppendLine("|---|---:|---:|");
        foreach (IGrouping<string, (CasoDePii Caso, string[] Obtido)> grupo in resultados.GroupBy(x => x.Caso.Tipo))
        {
            md.AppendLine(c, $"| {grupo.Key} | {grupo.Count()} | {grupo.Count(x => x.Obtido.SequenceEqual(x.Caso.Esperado.Order(StringComparer.Ordinal)))} |");
        }

        md.AppendLine()
            .AppendLine("## Casos")
            .AppendLine()
            .AppendLine("| # | Tipo | Caso | Campo | Esperado | Obtido | |")
            .AppendLine("|---:|---|---|---|---|---|---|");
        int n = 1;
        foreach ((CasoDePii caso, string[] obtido) in resultados)
        {
            bool ok = obtido.SequenceEqual(caso.Esperado.Order(StringComparer.Ordinal));
            md.AppendLine(c, $"| {n++} | {caso.Tipo} | {caso.Nome} | {(caso.Chave is null ? "texto livre" : $"`{caso.Chave}`")} | {Lista(caso.Esperado)} | {Lista(obtido)} | {(ok ? "✅" : "❌")} |");
        }

        return md.ToString();

        static string Lista(IEnumerable<string> ids) => ids.Any() ? string.Join(", ", ids.Select(i => $"`{i}`")) : "nenhum";
    }
}
