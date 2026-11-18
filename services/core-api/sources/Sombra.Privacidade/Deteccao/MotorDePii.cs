using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Deteccao;

/// <summary>
/// O motor de parsing de PII (spec 07, §1): aplica as regras do <c>pii-regras.json</c> a um valor, pelo nome do campo e
/// pelo conteúdo, e devolve as ocorrências sem sobreposição. Não guarda nem devolve o valor achado.
/// </summary>
public sealed class MotorDePii
{
    /// <summary>O tempo máximo de cada regex num valor: proteção contra ReDoS.</summary>
    public static readonly TimeSpan TempoMaximoPorRegex = TimeSpan.FromSeconds(2);

    private static readonly Lazy<MotorDePii> _padrao = new(() => new MotorDePii(CarregadorDeRegras.CarregarPadrao()));

    private readonly IReadOnlyList<IDetectorDePii> _detectores;
    private readonly IReadOnlyList<(RegraDePii Regra, string[][] Chaves)> _porChave;

    /// <summary>Initializes a new instance of the <see cref="MotorDePii"/> class.</summary>
    /// <param name="regras">As regras, já validadas pelo <see cref="CarregadorDeRegras"/>.</param>
    public MotorDePii(IReadOnlyList<RegraDePii> regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        Regras = regras;
        _detectores = [.. regras.Select(CriarDetector).OfType<IDetectorDePii>()];
        _porChave = [.. regras.Where(r => r.Chaves.Count > 0).Select(r => (r, r.Chaves.Select(ChaveDeCampo.Palavras).ToArray()))];
    }

    /// <summary>Gets o motor com as regras padrão embarcadas.</summary>
    public static MotorDePii Padrao => _padrao.Value;

    /// <summary>Gets as regras em uso.</summary>
    public IReadOnlyList<RegraDePii> Regras { get; }

    /// <summary>A forma do texto que o motor analisa (NFC). As posições das ocorrências valem nela.</summary>
    /// <param name="texto">O texto recebido.</param>
    /// <returns>O texto em NFC.</returns>
    public static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        return texto.IsNormalized(NormalizationForm.FormC) ? texto : texto.Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Analisa um valor. Se o nome do campo denuncia o dado (<c>cpf</c>, <c>laudo</c>, <c>senha</c>), o valor inteiro é
    /// uma ocorrência só, da regra mais severa. Senão, cada detector procura no conteúdo e as sobreposições ficam com a
    /// mais severa (depois a mais longa).
    /// </summary>
    /// <param name="texto">O valor.</param>
    /// <param name="chave">O nome do campo, quando o valor vem de um JSON ou formulário.</param>
    /// <returns>As ocorrências, em ordem de posição.</returns>
    public IReadOnlyList<OcorrenciaDePii> Analisar(string texto, string? chave = null)
    {
        ArgumentNullException.ThrowIfNull(texto);
        string normalizado = Normalizar(texto);
        if (string.IsNullOrWhiteSpace(normalizado))
        {
            return [];
        }

        if (chave is not null && RegraDaChave(chave) is { } regra)
        {
            return [new OcorrenciaDePii(regra.Id, regra.Categoria, regra.Severidade, OrigemDaDeteccao.Chave, 0, normalizado.Length)];
        }

        List<OcorrenciaDePii> escolhidas = [];
        IEnumerable<OcorrenciaDePii> candidatas = _detectores
            .SelectMany(d => d.Detectar(normalizado))
            .OrderByDescending(o => o.Severidade)
            .ThenByDescending(o => o.Tamanho)
            .ThenBy(o => o.Inicio);
        foreach (OcorrenciaDePii candidata in candidatas)
        {
            if (!escolhidas.Any(candidata.Sobrepoe))
            {
                escolhidas.Add(candidata);
            }
        }

        return [.. escolhidas.OrderBy(o => o.Inicio)];
    }

    /// <summary>Diz se um valor tem algum dado pessoal.</summary>
    /// <param name="texto">O valor.</param>
    /// <param name="chave">O nome do campo, quando houver.</param>
    /// <returns><c>true</c> quando há pelo menos uma ocorrência.</returns>
    public bool ContemPii(string texto, string? chave = null) => Analisar(texto, chave).Count > 0;

    /// <summary>A regra mais severa cujo nome de campo bate com a chave, se houver.</summary>
    /// <param name="chave">O nome do campo.</param>
    /// <returns>A regra, ou <c>null</c>.</returns>
    public RegraDePii? RegraDaChave(string chave)
    {
        ArgumentNullException.ThrowIfNull(chave);
        string[] palavras = ChaveDeCampo.Palavras(chave);
        return _porChave
            .Where(p => p.Chaves.Any(c => ChaveDeCampo.Contem(palavras, c)))
            .Select(p => p.Regra)
            .OrderByDescending(r => r.Severidade)
            .FirstOrDefault();
    }

    private static IDetectorDePii? CriarDetector(RegraDePii regra) => regra switch
    {
        { Termos.Count: > 0 } => new DetectorDeDicionario(regra),
        { Padrao: not null } => new DetectorPorPadrao(regra),
        _ => null,
    };
}
