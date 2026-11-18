using System;
using System.Security.Cryptography;
using System.Text;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Sinks;

namespace Sombra.Privacidade.Traducao;

/// <summary>
/// O que a IA pode saber de um evento de segurança: só metadados de uma lista fechada (spec 07 §3). Não tem valor, nome,
/// casa, correlação nem detalhe livre. A <see cref="Assinatura"/> junta eventos iguais para o cache.
/// </summary>
/// <param name="Tipo">O tipo (<c>ACESSO_NEGADO</c>, <c>VAZAMENTO_BLOQUEADO</c>, <c>MQTT_REPLAY</c>...).</param>
/// <param name="Sink">Por onde o dado tentou sair, quando é um evento de privacidade.</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">A severidade da regra.</param>
/// <param name="Regra">A regra que disparou.</param>
/// <param name="Onde">A rota-modelo, o evento de log, o host ou o tópico (o prompt só usa se tiver forma de identificador).</param>
/// <param name="Bloqueado">Se o Sombra bloqueou.</param>
/// <param name="Repeticoes">Quantos eventos iguais nas últimas 24 h.</param>
public sealed record EventoParaTraduzir(
    string Tipo,
    SinkDePii? Sink,
    CategoriaDePii Categoria,
    SeveridadeDePii Severidade,
    string? Regra,
    string Onde,
    bool Bloqueado,
    int Repeticoes = 1)
{
    /// <summary>Gets a assinatura do evento (sem as repetições): eventos com a mesma assinatura recebem a mesma tradução.</summary>
    public string Assinatura => De(Tipo, Sink, Categoria, Severidade, Regra, Onde, Bloqueado);

    /// <summary>Calcula a assinatura de um evento.</summary>
    /// <param name="tipo">O tipo.</param>
    /// <param name="sink">O sink.</param>
    /// <param name="categoria">A família.</param>
    /// <param name="severidade">A severidade.</param>
    /// <param name="regra">A regra.</param>
    /// <param name="onde">Onde.</param>
    /// <param name="bloqueado">Se foi bloqueado.</param>
    /// <returns>64 caracteres hexadecimais.</returns>
    public static string De(string tipo, SinkDePii? sink, CategoriaDePii categoria, SeveridadeDePii severidade, string? regra, string onde, bool bloqueado)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\n', tipo, sink?.ToString() ?? "-", categoria, severidade, regra ?? "-", onde, bloqueado ? "bloqueado" : "vazado"))));
}
