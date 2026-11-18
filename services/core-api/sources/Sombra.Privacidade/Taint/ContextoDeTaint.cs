using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Taint;

/// <summary>
/// O taint de uma requisição (spec 07 §2): os valores que entraram marcados como PII e as detecções. Os valores ficam
/// só em memória, durante a requisição, para os guardas de sink reconhecerem o dado se ele tentar sair. Nunca são
/// serializados nem logados.
/// </summary>
public sealed class ContextoDeTaint
{
    /// <summary>Valores mais curtos que isto não são marcados: "sim" ou "11" bateriam em qualquer texto.</summary>
    public const int TamanhoMinimo = 4;

    private static readonly AsyncLocal<ContextoDeTaint?> _atual = new();

    private readonly Lock _trava = new();
    private readonly Dictionary<string, (string RegraId, CategoriaDePii Categoria)> _valores = new(StringComparer.Ordinal);
    private readonly List<DeteccaoDePii> _deteccoes = [];

    /// <summary>
    /// Gets or sets o taint da requisição em andamento. Os guardas que não vivem no escopo da requisição (o sink de log)
    /// leem daqui; fora de uma requisição (workers), é <c>null</c> e só a detecção por conteúdo vale.
    /// </summary>
    public static ContextoDeTaint? Atual
    {
        get => _atual.Value;
        set => _atual.Value = value;
    }

    /// <summary>Gets as detecções, na ordem em que aconteceram.</summary>
    public IReadOnlyList<DeteccaoDePii> Deteccoes
    {
        get
        {
            lock (_trava)
            {
                return [.. _deteccoes];
            }
        }
    }

    /// <summary>Gets quantos valores distintos estão marcados.</summary>
    public int ValoresMarcados
    {
        get
        {
            lock (_trava)
            {
                return _valores.Count;
            }
        }
    }

    /// <summary>Marca um valor como PII e registra a detecção.</summary>
    /// <param name="valor">O dado (o campo inteiro ou o trecho achado).</param>
    /// <param name="deteccao">Os metadados.</param>
    public void Marcar(string valor, DeteccaoDePii deteccao)
    {
        ArgumentNullException.ThrowIfNull(deteccao);
        lock (_trava)
        {
            _deteccoes.Add(deteccao);
        }

        MarcarValor(valor, deteccao.RegraId, deteccao.Categoria);
    }

    /// <summary>Marca um trecho sem registrar detecção (o JWT dentro de um <c>Bearer</c> já detectado pela chave).</summary>
    /// <param name="valor">O trecho.</param>
    /// <param name="regraId">A regra que reconheceu o trecho.</param>
    /// <param name="categoria">A família do dado.</param>
    public void MarcarValor(string valor, string regraId, CategoriaDePii categoria)
    {
        ArgumentNullException.ThrowIfNull(valor);
        string limpo = valor.Trim();
        string digitos = new([.. limpo.Where(char.IsAsciiDigit)]);
        lock (_trava)
        {
            if (limpo.Length >= TamanhoMinimo)
            {
                _valores.TryAdd(limpo, (regraId, categoria));
            }

            // CPF digitado com pontuação pode sair sem ela (e vice-versa): os dígitos também ficam marcados.
            if (digitos.Length >= 8 && digitos.Length * 2 >= limpo.Length)
            {
                _valores.TryAdd(digitos, (regraId, categoria));
            }
        }
    }

    /// <summary>Diz se um texto carrega algum valor marcado.</summary>
    /// <param name="texto">O texto a caminho de um sink (log, resposta, saída externa).</param>
    /// <returns><c>true</c> quando algum valor marcado aparece no texto.</returns>
    public bool Contem(string? texto) => Achar(texto).Count > 0;

    /// <summary>As regras dos valores marcados que aparecem no texto.</summary>
    /// <param name="texto">O texto a caminho de um sink.</param>
    /// <returns>Regra e família de cada valor achado (sem o valor).</returns>
    public IReadOnlyList<(string RegraId, CategoriaDePii Categoria)> Achar(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return [];
        }

        lock (_trava)
        {
            return [.. _valores.Where(v => texto.Contains(v.Key, StringComparison.Ordinal)).Select(v => v.Value).Distinct()];
        }
    }

    /// <summary>Troca cada valor marcado que aparece no texto por <c>[PII:regra]</c>, do mais longo ao mais curto.</summary>
    /// <param name="texto">O texto.</param>
    /// <returns>O texto sem nenhum valor marcado.</returns>
    public string Redigir(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        lock (_trava)
        {
            foreach (KeyValuePair<string, (string RegraId, CategoriaDePii Categoria)> v in _valores.OrderByDescending(v => v.Key.Length))
            {
                texto = texto.Replace(v.Key, $"[PII:{v.Value.RegraId}]", StringComparison.Ordinal);
            }

            return texto;
        }
    }
}
