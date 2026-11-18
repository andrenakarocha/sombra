using System;
using System.Diagnostics.CodeAnalysis;
using Sombra.Dominio.Calor;

namespace Sombra.Dominio.Sentinelas;

/// <summary>
/// Uma medição de dentro da casa, feita pela Sentinela. Não confundir com a observação da estação, que é do Clima
/// (glossário: Leitura × Observação).
/// </summary>
public sealed class Leitura
{
    internal Leitura(string deviceId, DateTimeOffset momento, Temperatura temperatura, UmidadeRelativa umidade, LimitesTermicos limites)
    {
        Id = Guid.CreateVersion7();
        DeviceId = deviceId;
        Momento = momento.ToUniversalTime();
        Temperatura = temperatura;
        Umidade = umidade;
        IndiceDeCalor = IndiceDeCalor.Calcular(temperatura, umidade);
        Nivel = limites.Classificar(IndiceDeCalor);
    }

    /// <summary>
    /// Reconstitui uma leitura gravada (usado pelo EF Core). O índice de calor é recalculado, e o nível é o gravado,
    /// porque os limites da casa podem ter mudado depois.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Só o EF Core chama; coberto pelos testes de integração.")]
    private Leitura(Guid id, string deviceId, DateTimeOffset momento, Temperatura temperatura, UmidadeRelativa umidade, NivelDeRisco nivel)
    {
        Id = id;
        DeviceId = deviceId;
        Momento = momento;
        Temperatura = temperatura;
        Umidade = umidade;
        IndiceDeCalor = IndiceDeCalor.Calcular(temperatura, umidade);
        Nivel = nivel;
    }

    /// <summary>Gets o identificador da leitura.</summary>
    public Guid Id { get; }

    /// <summary>Gets a Sentinela que mediu.</summary>
    public string DeviceId { get; }

    /// <summary>Gets o instante da medição, em UTC.</summary>
    public DateTimeOffset Momento { get; }

    /// <summary>Gets a temperatura do ar.</summary>
    public Temperatura Temperatura { get; }

    /// <summary>Gets a umidade relativa.</summary>
    public UmidadeRelativa Umidade { get; }

    /// <summary>Gets o índice de calor calculado pelo Core (a Sentinela também calcula, mas é informativo; ADR-006).</summary>
    public IndiceDeCalor IndiceDeCalor { get; }

    /// <summary>Gets o nível pelos limites pessoais do domicílio no momento da leitura.</summary>
    public NivelDeRisco Nivel { get; }
}
