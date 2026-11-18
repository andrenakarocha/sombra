using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Sombra.Privacidade.Taint;

/// <summary>
/// O taint de uma requisição (spec 07 §2): os valores que entraram marcados como PII e as detecções. Os valores ficam
/// só em memória, durante a requisição, para os guardas de sink (F2.4) reconhecerem o dado se ele tentar sair. Nunca
/// são serializados nem logados.
/// </summary>
public sealed class ContextoDeTaint
{
    /// <summary>Valores mais curtos que isto não são marcados: "sim" ou "11" bateriam em qualquer texto.</summary>
    public const int TamanhoMinimo = 4;

    private readonly Lock _trava = new();
    private readonly HashSet<string> _valores = new(StringComparer.Ordinal);
    private readonly List<DeteccaoDePii> _deteccoes = [];

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

        MarcarValor(valor);
    }

    /// <summary>Marca um trecho sem registrar detecção (o JWT dentro de um <c>Bearer</c> já detectado pela chave).</summary>
    /// <param name="valor">O trecho.</param>
    public void MarcarValor(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        string limpo = valor.Trim();
        string digitos = new([.. limpo.Where(char.IsAsciiDigit)]);
        lock (_trava)
        {
            if (limpo.Length >= TamanhoMinimo)
            {
                _valores.Add(limpo);
            }

            // CPF digitado com pontuação pode sair sem ela (e vice-versa): os dígitos também ficam marcados.
            if (digitos.Length >= 8 && digitos.Length * 2 >= limpo.Length)
            {
                _valores.Add(digitos);
            }
        }
    }

    /// <summary>Diz se um texto carrega algum valor marcado.</summary>
    /// <param name="texto">O texto a caminho de um sink (log, resposta, saída externa).</param>
    /// <returns><c>true</c> quando algum valor marcado aparece no texto.</returns>
    public bool Contem(string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return false;
        }

        lock (_trava)
        {
            return _valores.Any(v => texto.Contains(v, StringComparison.Ordinal));
        }
    }
}
