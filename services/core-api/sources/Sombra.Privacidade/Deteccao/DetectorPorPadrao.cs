using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// Detector por padrão: a regex da regra acha candidatos e o validador nomeado confirma cada um (dígito verificador do
/// CPF, DDD do telefone, entropia do segredo). Atende CPF, Cartão SUS, RG, telefone, e-mail, CEP e credenciais, e
/// qualquer regra nova que só precise de regex e validador.
/// </summary>
public sealed class DetectorPorPadrao : IDetectorDePii
{
    /// <summary>
    /// O grupo nomeado que limita a ocorrência ao dado: em <c>Bearer (?&lt;dado&gt;...)</c>, o validador vê o match
    /// inteiro, mas só o token é marcado e mascarado.
    /// </summary>
    public const string GrupoDoDado = "dado";

    private readonly Regex _padrao;
    private readonly Func<string, bool> _validador;

    /// <summary>Initializes a new instance of the <see cref="DetectorPorPadrao"/> class.</summary>
    /// <param name="regra">Uma regra com <c>padrao</c>.</param>
    public DetectorPorPadrao(RegraDePii regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentException.ThrowIfNullOrWhiteSpace(regra.Padrao);
        if (!Validadores.TentarObter(regra.Validador, out Func<string, bool> validador))
        {
            throw new ArgumentException($"validador '{regra.Validador}' desconhecido", nameof(regra));
        }

        Regra = regra;
        _padrao = new Regex(regra.Padrao, RegexOptions.Compiled | RegexOptions.CultureInvariant, MotorDePii.TempoMaximoPorRegex);
        _validador = validador;
    }

    /// <inheritdoc/>
    public RegraDePii Regra { get; }

    /// <inheritdoc/>
    public IEnumerable<OcorrenciaDePii> Detectar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        foreach (Match m in _padrao.Matches(texto))
        {
            if (_validador(m.Value))
            {
                Group dado = m.Groups[GrupoDoDado];
                Capture trecho = dado.Success ? dado : m;
                yield return new OcorrenciaDePii(Regra.Id, Regra.Categoria, Regra.Severidade, OrigemDaDeteccao.Conteudo, trecho.Index, trecho.Length);
            }
        }
    }
}
