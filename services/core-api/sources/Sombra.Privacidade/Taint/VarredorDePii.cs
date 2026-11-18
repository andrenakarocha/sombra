using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Sombra.Privacidade.Deteccao;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Taint;

/// <summary>
/// Percorre um JSON de entrada e classifica cada texto por chave e por conteúdo (spec 07 §1):
/// <list type="bullet">
/// <item>campo de PII pela chave (<c>cpf</c>, <c>telefone</c>, <c>senha</c>): o valor inteiro é marcado no taint e segue;</item>
/// <item>campo livre (sem regra pela chave, ou com regra <c>campoLivre</c>, como o perfil de saúde): dado pessoal de outra
/// família achado no conteúdo está fora do lugar e cai na <see cref="PoliticaDeCampoLivre"/>.</item>
/// </list>
/// </summary>
public sealed class VarredorDePii
{
    private readonly MotorDePii _motor;
    private readonly PoliticaDeCampoLivre _politica;

    /// <summary>Initializes a new instance of the <see cref="VarredorDePii"/> class.</summary>
    /// <param name="motor">O motor de PII.</param>
    /// <param name="politica">O que fazer com dado fora do lugar.</param>
    public VarredorDePii(MotorDePii motor, PoliticaDeCampoLivre politica)
    {
        _motor = motor ?? throw new ArgumentNullException(nameof(motor));
        _politica = politica;
    }

    /// <summary>Varre um JSON, marca o taint e, na política <see cref="PoliticaDeCampoLivre.Mascarar"/>, mascara no lugar.</summary>
    /// <param name="raiz">O JSON (é alterado quando há algo a mascarar).</param>
    /// <param name="contexto">O taint da requisição.</param>
    /// <returns>As detecções fora do lugar (mascaradas ou a recusar). Vazio quando nada mudou.</returns>
    public IReadOnlyList<DeteccaoDePii> VarrerJson(JsonNode? raiz, ContextoDeTaint contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        List<DeteccaoDePii> foraDoLugar = [];
        Visitar(raiz, chave: null, "$", contexto, foraDoLugar);
        return foraDoLugar;
    }

    /// <summary>Varre um valor solto (query string ou header): só marca o taint, nunca reescreve.</summary>
    /// <param name="valor">O valor.</param>
    /// <param name="chave">O nome do parâmetro.</param>
    /// <param name="fonte">Query ou header.</param>
    /// <param name="contexto">O taint da requisição.</param>
    public void MarcarValor(string valor, string chave, FonteDePii fonte, ContextoDeTaint contexto)
    {
        ArgumentNullException.ThrowIfNull(valor);
        ArgumentNullException.ThrowIfNull(chave);
        ArgumentNullException.ThrowIfNull(contexto);
        string campo = $"{(fonte == FonteDePii.Query ? "query" : "header")}.{Segmento(chave)}";
        string normalizado = MotorDePii.Normalizar(valor);
        foreach (OcorrenciaDePii o in _motor.Analisar(normalizado, chave))
        {
            contexto.Marcar(normalizado.Substring(o.Inicio, o.Tamanho), Deteccao(fonte, campo, o, DesfechoDaDeteccao.Marcado));
        }

        MarcarTrechos(normalizado, contexto);
    }

    private void Visitar(JsonNode? no, string? chave, string caminho, ContextoDeTaint contexto, List<DeteccaoDePii> foraDoLugar)
    {
        switch (no)
        {
            case JsonObject objeto:
                foreach (KeyValuePair<string, JsonNode?> propriedade in objeto.ToList())
                {
                    string filho = $"{caminho}.{Segmento(propriedade.Key)}";
                    if (propriedade.Value is JsonValue v && v.TryGetValue(out string? texto))
                    {
                        objeto[propriedade.Key] = Texto(texto, propriedade.Key, filho, contexto, foraDoLugar);
                    }
                    else
                    {
                        Visitar(propriedade.Value, propriedade.Key, filho, contexto, foraDoLugar);
                    }
                }

                break;

            case JsonArray lista:
                for (int i = 0; i < lista.Count; i++)
                {
                    string filho = $"{caminho}[{i}]";
                    if (lista[i] is JsonValue v && v.TryGetValue(out string? texto))
                    {
                        lista[i] = Texto(texto, chave, filho, contexto, foraDoLugar);
                    }
                    else
                    {
                        Visitar(lista[i], chave, filho, contexto, foraDoLugar);
                    }
                }

                break;

            default:
                break;
        }
    }

    private string Texto(string valor, string? chave, string campo, ContextoDeTaint contexto, List<DeteccaoDePii> foraDoLugar)
    {
        string normalizado = MotorDePii.Normalizar(valor);
        RegraDePii? daChave = chave is null ? null : _motor.RegraDaChave(chave);
        if (daChave is not null && !string.IsNullOrWhiteSpace(normalizado))
        {
            contexto.Marcar(normalizado, new DeteccaoDePii(FonteDePii.Corpo, campo, daChave.Id, daChave.Categoria, daChave.Severidade, OrigemDaDeteccao.Chave, DesfechoDaDeteccao.Marcado));
            if (!daChave.CampoLivre)
            {
                MarcarTrechos(normalizado, contexto);
                return valor;
            }
        }

        OcorrenciaDePii[] achadas = [.. _motor.AnalisarConteudo(normalizado, chave).Where(o => daChave is null || o.Categoria != daChave.Categoria)];
        if (achadas.Length == 0)
        {
            return valor;
        }

        DesfechoDaDeteccao desfecho = _politica == PoliticaDeCampoLivre.Mascarar ? DesfechoDaDeteccao.Mascarado : DesfechoDaDeteccao.Recusado;
        foreach (OcorrenciaDePii o in achadas)
        {
            DeteccaoDePii deteccao = Deteccao(FonteDePii.Corpo, campo, o, desfecho);
            contexto.Marcar(normalizado.Substring(o.Inicio, o.Tamanho), deteccao);
            foraDoLugar.Add(deteccao);
        }

        return desfecho == DesfechoDaDeteccao.Mascarado ? _motor.Mascarar(normalizado, achadas) : valor;
    }

    // Num campo de PII pela chave, o valor inteiro já está marcado; os trechos que o conteúdo reconhece (o JWT dentro do
    // "Bearer ...") também ficam, porque podem sair sozinhos.
    private void MarcarTrechos(string normalizado, ContextoDeTaint contexto)
    {
        foreach (OcorrenciaDePii o in _motor.Analisar(normalizado))
        {
            contexto.MarcarValor(normalizado.Substring(o.Inicio, o.Tamanho));
        }
    }

    private static DeteccaoDePii Deteccao(FonteDePii fonte, string campo, OcorrenciaDePii o, DesfechoDaDeteccao desfecho)
        => new(fonte, campo, o.RegraId, o.Categoria, o.Severidade, o.Origem, desfecho);

    // O nome do campo vai para log e evento: se ele mesmo tiver PII (uma chave "12345678909"), sai como [PII].
    private string Segmento(string chave) => _motor.ContemPii(chave) ? "[PII]" : chave;
}
