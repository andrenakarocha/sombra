using System;
using System.Collections.Generic;
using System.Linq;
using Sombra.Privacidade.Regras;

namespace Sombra.Privacidade.Risco;

/// <summary>A cor do farol de risco do cidadão.</summary>
public enum FarolDeRisco
{
    /// <summary>Pontuação abaixo de 30.</summary>
    Verde,

    /// <summary>De 30 a menos de 70.</summary>
    Amarelo,

    /// <summary>70 ou mais.</summary>
    Vermelho,
}

/// <summary>Um evento de segurança visto pelo farol: quando, que família, que peso e se foi bloqueado ou vazou.</summary>
/// <param name="Momento">Quando aconteceu.</param>
/// <param name="Categoria">A família do dado.</param>
/// <param name="Severidade">O peso da regra.</param>
/// <param name="Bloqueado">Se o Sombra bloqueou (com o módulo ligado, sempre; vazado só no modo inseguro da demo).</param>
public sealed record EventoDeRisco(DateTimeOffset Momento, CategoriaDePii Categoria, SeveridadeDePii Severidade, bool Bloqueado);

/// <summary>O farol calculado.</summary>
/// <param name="Pontuacao">De 0 a 100.</param>
/// <param name="Farol">A cor.</param>
/// <param name="PorCategoria">A pontuação de cada família que teve evento na janela.</param>
/// <param name="Eventos">Quantos eventos entraram na conta.</param>
public sealed record AvaliacaoDeRisco(double Pontuacao, FarolDeRisco Farol, IReadOnlyDictionary<CategoriaDePii, double> PorCategoria, int Eventos);

/// <summary>
/// O cálculo do Risco Cidadão (spec 07 §4): cada evento da janela vale
/// <c>40 × peso da família × peso da severidade × peso do desfecho × 0,5^(idade / meia-vida)</c>, e a soma (até 100)
/// dá a cor: verde &lt; 30 ≤ amarelo &lt; 70 ≤ vermelho. Calibrado para a leitura que o app já usava: um bloqueio de saúde
/// recente acende o amarelo, três acendem o vermelho, e um vazamento de saúde acende o vermelho sozinho.
/// </summary>
public static class RiscoCidadao
{
    /// <summary>Só os eventos desta janela contam.</summary>
    public static readonly TimeSpan Janela = TimeSpan.FromDays(7);

    /// <summary>Em quanto tempo um evento passa a valer metade.</summary>
    public static readonly TimeSpan MeiaVida = TimeSpan.FromHours(72);

    /// <summary>A partir daqui, amarelo.</summary>
    public const double LimiteAmarelo = 30;

    /// <summary>A partir daqui, vermelho.</summary>
    public const double LimiteVermelho = 70;

    private const double PontosPorEvento = 40;
    private const double PesoDoVazamento = 3;

    private static readonly Dictionary<CategoriaDePii, double> _pesoDaCategoria = new()
    {
        [CategoriaDePii.Saude] = 1.0,
        [CategoriaDePii.Credencial] = 1.0,
        [CategoriaDePii.Identificacao] = 0.8,
        [CategoriaDePii.Contato] = 0.6,
        [CategoriaDePii.Localizacao] = 0.4,
    };

    private static readonly Dictionary<SeveridadeDePii, double> _pesoDaSeveridade = new()
    {
        [SeveridadeDePii.Baixa] = 0.4,
        [SeveridadeDePii.Media] = 0.6,
        [SeveridadeDePii.Alta] = 0.8,
        [SeveridadeDePii.Critica] = 1.0,
    };

    /// <summary>Quanto um evento vale agora (zero fora da janela ou no futuro).</summary>
    /// <param name="evento">O evento.</param>
    /// <param name="agora">O instante de referência.</param>
    /// <returns>Os pontos do evento.</returns>
    public static double Pontos(EventoDeRisco evento, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(evento);
        TimeSpan idade = agora - evento.Momento;
        if (idade < TimeSpan.Zero || idade > Janela)
        {
            return 0;
        }

        double decaimento = Math.Pow(0.5, idade / MeiaVida);
        return PontosPorEvento * _pesoDaCategoria[evento.Categoria] * _pesoDaSeveridade[evento.Severidade]
            * (evento.Bloqueado ? 1 : PesoDoVazamento) * decaimento;
    }

    /// <summary>A cor de uma pontuação.</summary>
    /// <param name="pontuacao">De 0 a 100.</param>
    /// <returns>A cor.</returns>
    public static FarolDeRisco FarolDe(double pontuacao)
        => pontuacao >= LimiteVermelho ? FarolDeRisco.Vermelho : pontuacao >= LimiteAmarelo ? FarolDeRisco.Amarelo : FarolDeRisco.Verde;

    /// <summary>Avalia um conjunto de eventos: no total e por família.</summary>
    /// <param name="eventos">Os eventos (os de fora da janela são ignorados).</param>
    /// <param name="agora">O instante de referência.</param>
    /// <returns>A avaliação.</returns>
    public static AvaliacaoDeRisco Avaliar(IEnumerable<EventoDeRisco> eventos, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        (EventoDeRisco Evento, double Pontos)[] validos = [.. eventos.Select(e => (e, Pontos(e, agora))).Where(e => e.Item2 > 0)];
        double total = Limitar(validos.Sum(e => e.Pontos));
        Dictionary<CategoriaDePii, double> porCategoria = validos
            .GroupBy(e => e.Evento.Categoria)
            .ToDictionary(g => g.Key, g => Limitar(g.Sum(e => e.Pontos)));
        return new AvaliacaoDeRisco(total, FarolDe(total), porCategoria, validos.Length);
    }

    private static double Limitar(double pontos) => Math.Round(Math.Min(100, pontos), 1);
}
