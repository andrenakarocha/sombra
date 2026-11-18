using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Privacidade.Regras;
using Sombra.Privacidade.Risco;
using Sombra.Privacidade.Sinks;

namespace Sombra.Aplicacao.Respostas;

/// <summary>A cor do farol no formato da API, a partir do cálculo do Risco Cidadão.</summary>
public static class Farois
{
    /// <summary>Converte a cor do cálculo na cor do contrato.</summary>
    /// <param name="farol">A cor calculada.</param>
    /// <returns>A cor do contrato.</returns>
    public static FarolDePrivacidade De(FarolDeRisco farol) => farol switch
    {
        FarolDeRisco.Vermelho => FarolDePrivacidade.Vermelho,
        FarolDeRisco.Amarelo => FarolDePrivacidade.Amarelo,
        _ => FarolDePrivacidade.Verde,
    };
}

/// <summary>O farol de uma família de dado.</summary>
/// <param name="Categoria">A família.</param>
/// <param name="Pontuacao">De 0 a 100.</param>
/// <param name="Farol">A cor.</param>
/// <param name="Eventos">Quantos eventos da família na janela.</param>
public sealed record RiscoPorCategoriaResposta(CategoriaDePii Categoria, double Pontuacao, FarolDePrivacidade Farol, int Eventos);

/// <summary>Uma contagem de eventos (por sink ou por regra).</summary>
/// <param name="Nome">O sink ou a regra.</param>
/// <param name="Eventos">Quantos eventos na janela.</param>
public sealed record ContagemDeEventosResposta(string Nome, int Eventos);

/// <summary>
/// O farol executivo de Risco Cidadão (contrato <c>GET /privacidade/risco-cidadao</c>, spec 07 §4): global e por família,
/// com as contagens por sink e as regras que mais dispararam na janela.
/// </summary>
/// <param name="Pontuacao">De 0 a 100.</param>
/// <param name="Farol">A cor global.</param>
/// <param name="EventosNaJanela">Quantos eventos entraram na conta.</param>
/// <param name="PorCategoria">O farol de cada família com evento, da pior para a melhor.</param>
/// <param name="PorSink">Os eventos de privacidade por sink.</param>
/// <param name="TopRegras">As regras que mais dispararam.</param>
/// <param name="CalculadoEm">O instante do cálculo.</param>
public sealed record RiscoCidadaoResposta(
    double Pontuacao,
    FarolDePrivacidade Farol,
    int EventosNaJanela,
    IReadOnlyList<RiscoPorCategoriaResposta> PorCategoria,
    IReadOnlyList<ContagemDeEventosResposta> PorSink,
    IReadOnlyList<ContagemDeEventosResposta> TopRegras,
    DateTimeOffset CalculadoEm)
{
    /// <summary>Quantas regras o ranking mostra.</summary>
    public const int MaximoDeRegras = 5;

    /// <summary>Calcula o farol a partir dos eventos da janela.</summary>
    /// <param name="eventos">Cada evento como o farol vê, com o sink e a regra (nulos nos eventos MQTT).</param>
    /// <param name="agora">O instante de referência.</param>
    /// <returns>O farol.</returns>
    public static RiscoCidadaoResposta De(IReadOnlyList<(EventoDeRisco Risco, SinkDePii? Sink, string? Regra)> eventos, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        (EventoDeRisco Risco, SinkDePii? Sink, string? Regra)[] naJanela = [.. eventos.Where(e => RiscoCidadao.Pontos(e.Risco, agora) > 0)];
        AvaliacaoDeRisco avaliacao = RiscoCidadao.Avaliar(naJanela.Select(e => e.Risco), agora);
        return new(
            avaliacao.Pontuacao,
            Farois.De(avaliacao.Farol),
            avaliacao.Eventos,
            [.. avaliacao.PorCategoria
                .OrderByDescending(c => c.Value).ThenBy(c => c.Key)
                .Select(c => new RiscoPorCategoriaResposta(c.Key, c.Value, Farois.De(RiscoCidadao.FarolDe(c.Value)), naJanela.Count(e => e.Risco.Categoria == c.Key)))],
            Contar(naJanela.Where(e => e.Sink is not null).Select(e => e.Sink!.Value.ToString()), int.MaxValue),
            Contar(naJanela.Where(e => e.Regra is not null).Select(e => e.Regra!), MaximoDeRegras),
            agora);
    }

    private static ContagemDeEventosResposta[] Contar(IEnumerable<string> nomes, int maximo)
        => [.. nomes.GroupBy(n => n, StringComparer.Ordinal)
            .Select(g => new ContagemDeEventosResposta(g.Key, g.Count()))
            .OrderByDescending(c => c.Eventos).ThenBy(c => c.Nome, StringComparer.Ordinal)
            .Take(maximo)];
}

/// <summary>Um evento da trilha de segurança, só com metadados (contrato <c>GET /privacidade/eventos</c>).</summary>
/// <param name="Id">O identificador.</param>
/// <param name="Momento">Quando.</param>
/// <param name="Tipo">O tipo (<c>ACESSO_NEGADO</c>, <c>VAZAMENTO_BLOQUEADO</c>, <c>MQTT_REPLAY</c>...).</param>
/// <param name="Origem">Onde: a rota-modelo, o evento de log, o host, o tópico ou a Sentinela.</param>
/// <param name="Sink">Por onde o dado tentou sair (nos eventos de privacidade).</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">A severidade da regra.</param>
/// <param name="Regra">A regra que disparou.</param>
/// <param name="Bloqueado">Se o Sombra bloqueou.</param>
/// <param name="DomicilioId">A casa, quando se sabe.</param>
/// <param name="CorrelationId">A correlação, para achar o resto nos logs.</param>
/// <param name="Detalhe">O resumo legível, sem dado pessoal.</param>
public sealed record EventoDeSegurancaResposta(
    Guid Id,
    DateTimeOffset Momento,
    string Tipo,
    string Origem,
    SinkDePii? Sink,
    CategoriaDePii? Categoria,
    SeveridadeDePii? Severidade,
    string? Regra,
    bool Bloqueado,
    Guid? DomicilioId,
    string? CorrelationId,
    string Detalhe);
