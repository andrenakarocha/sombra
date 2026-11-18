using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Taint;

namespace Sombra.Privacidade.Sinks;

/// <summary>
/// A "camada de validação antes das saídas" (spec 07 §2), comum a todos os sinks: confere um conteúdo contra o taint da
/// requisição (o dado que entrou) e contra o motor de PII (o dado que veio do banco ou de outro lugar), e devolve o que
/// achou e uma versão redigida (<c>[PII:regra]</c>). Um JSON é conferido campo a campo, para as exceções por chave
/// (<c>sig</c>) valerem.
/// </summary>
public sealed class GuardaDeSaida
{
    private readonly MotorDePii _motor;
    private readonly Dictionary<string, SeveridadeDePii> _severidades;

    /// <summary>Initializes a new instance of the <see cref="GuardaDeSaida"/> class.</summary>
    /// <param name="motor">O motor de PII.</param>
    public GuardaDeSaida(MotorDePii motor)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _severidades = motor.Regras.ToDictionary(r => r.Id, r => r.Severidade, StringComparer.Ordinal);
    }

    /// <summary>Confere um conteúdo a caminho de um sink.</summary>
    /// <param name="conteudo">O texto ou JSON.</param>
    /// <param name="taint">O taint da requisição, quando houver.</param>
    /// <param name="chave">O nome do campo (a propriedade do log): com ele, a regra pela chave também vale.</param>
    /// <returns>Os achados e o conteúdo redigido.</returns>
    public ResultadoDaGuarda Verificar(string? conteudo, ContextoDeTaint? taint, string? chave = null)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            return new ResultadoDaGuarda([], conteudo ?? string.Empty);
        }

        List<AchadoNaSaida> achados = [];
        string redigido = ComoJson(conteudo) is { } raiz
            ? VerificarJson(raiz, taint, achados) ? raiz.ToJsonString() : conteudo
            : VerificarTexto(conteudo, chave, usarChave: chave is not null, taint, achados);
        return new ResultadoDaGuarda([.. achados.Distinct()], redigido);
    }

    private static JsonNode? ComoJson(string conteudo)
    {
        char primeiro = conteudo.TrimStart()[0];
        if (primeiro is not ('{' or '['))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(conteudo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool VerificarJson(JsonNode raiz, ContextoDeTaint? taint, List<AchadoNaSaida> achados)
    {
        bool mudou = false;
        Visitar(raiz, null);
        return mudou;

        void Visitar(JsonNode? no, string? chave)
        {
            switch (no)
            {
                case JsonObject objeto:
                    foreach (KeyValuePair<string, JsonNode?> p in objeto.ToList())
                    {
                        if (Texto(p.Value, p.Key) is { } novo)
                        {
                            objeto[p.Key] = novo;
                        }
                        else
                        {
                            Visitar(p.Value, p.Key);
                        }
                    }

                    break;
                case JsonArray lista:
                    for (int i = 0; i < lista.Count; i++)
                    {
                        if (Texto(lista[i], chave) is { } novo)
                        {
                            lista[i] = novo;
                        }
                        else
                        {
                            Visitar(lista[i], chave);
                        }
                    }

                    break;
                default:
                    break;
            }
        }

        string? Texto(JsonNode? no, string? chave)
        {
            if (no is not JsonValue valor || !valor.TryGetValue(out string? texto))
            {
                return null;
            }

            int antes = achados.Count;
            string redigido = VerificarTexto(texto, chave, usarChave: false, taint, achados);
            mudou |= achados.Count > antes;
            return achados.Count > antes ? redigido : null;
        }
    }

    private string VerificarTexto(string texto, string? chave, bool usarChave, ContextoDeTaint? taint, List<AchadoNaSaida> achados)
    {
        string normalizado = MotorDePii.Normalizar(texto);
        if (usarChave && chave is not null && _motor.RegraDaChave(chave) is { } regra)
        {
            achados.Add(new AchadoNaSaida(regra.Id, regra.Categoria, regra.Severidade, PeloTaint: false));
            return $"[PII:{regra.Id}]";
        }

        if (taint is not null)
        {
            IReadOnlyList<(string RegraId, CategoriaDePii Categoria)> marcados = taint.Achar(normalizado);
            if (marcados.Count > 0)
            {
                achados.AddRange(marcados.Select(m => new AchadoNaSaida(m.RegraId, m.Categoria, Severidade(m.RegraId), PeloTaint: true)));
                normalizado = taint.Redigir(normalizado);
            }
        }

        IReadOnlyList<OcorrenciaDePii> ocorrencias = _motor.AnalisarConteudo(normalizado, chave);
        if (ocorrencias.Count == 0)
        {
            return normalizado;
        }

        achados.AddRange(ocorrencias.Select(o => new AchadoNaSaida(o.RegraId, o.Categoria, o.Severidade, PeloTaint: false)));
        StringBuilder resultado = new(normalizado);
        foreach (OcorrenciaDePii o in ocorrencias.OrderByDescending(o => o.Inicio))
        {
            resultado.Remove(o.Inicio, o.Tamanho).Insert(o.Inicio, $"[PII:{o.RegraId}]");
        }

        return resultado.ToString();
    }

    private SeveridadeDePii Severidade(string regraId) => _severidades.GetValueOrDefault(regraId, SeveridadeDePii.Alta);
}
