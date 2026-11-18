using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Detector de dicionário: procura os <c>termos</c> da regra como palavras inteiras, sem ligar para caixa e acentos
/// ("HIPERTENSAO" bate com "hipertensão"), e aplica também o <c>padrao</c>, quando houver (os códigos CID-10 da regra
/// de saúde).
/// </summary>
public sealed class DetectorDeDicionario : IDetectorDePii
{
    private readonly Regex? _termos;
    private readonly Regex? _padrao;

    /// <summary>Initializes a new instance of the <see cref="DetectorDeDicionario"/> class.</summary>
    /// <param name="regra">Uma regra com <c>termos</c>.</param>
    public DetectorDeDicionario(RegraDePii regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        if (regra.Termos.Count == 0)
        {
            throw new ArgumentException("a regra não tem termos", nameof(regra));
        }

        Regra = regra;
        string alternativas = string.Join('|', regra.Termos
            .Select(TextoDobrado.Dobrar)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(t => t.Length)
            .Select(Regex.Escape));
        _termos = new Regex($@"(?<![\p{{L}}\p{{N}}])(?:{alternativas})(?![\p{{L}}\p{{N}}])", RegexOptions.Compiled | RegexOptions.CultureInvariant, MotorDePii.TempoMaximoPorRegex);
        if (regra.Padrao is not null)
        {
            _padrao = new Regex(regra.Padrao, RegexOptions.Compiled | RegexOptions.CultureInvariant, MotorDePii.TempoMaximoPorRegex);
        }
    }

    /// <inheritdoc/>
    public RegraDePii Regra { get; }

    /// <inheritdoc/>
    public IEnumerable<OcorrenciaDePii> Detectar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        IEnumerable<Match> achados = _termos!.Matches(TextoDobrado.Dobrar(texto));
        if (_padrao is not null)
        {
            achados = achados.Concat(_padrao.Matches(texto));
        }

        return achados.Select(m => new OcorrenciaDePii(Regra.Id, Regra.Categoria, Regra.Severidade, OrigemDaDeteccao.Conteudo, m.Index, m.Length));
    }
}
