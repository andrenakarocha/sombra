using System;
using Sombra.Dominio.Comum;

namespace Sombra.Dominio.Geo;

/// <summary>Uma posição na Terra, em graus decimais (WGS 84).</summary>
public sealed record PontoGeografico
{
    private const double RaioDaTerraKm = 6371.0088;

    private PontoGeografico(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Gets a latitude, de −90 a 90.</summary>
    public double Latitude { get; }

    /// <summary>Gets a longitude, de −180 a 180.</summary>
    public double Longitude { get; }

    /// <summary>Cria o ponto, recusando coordenada fora da Terra.</summary>
    /// <param name="latitude">A latitude.</param>
    /// <param name="longitude">A longitude.</param>
    /// <returns>O ponto.</returns>
    /// <exception cref="RegraVioladaException">Coordenada fora da faixa.</exception>
    public static PontoGeografico Criar(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsNaN(longitude) || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180)
        {
            throw new RegraVioladaException("CADASTRO", "Coordenada fora da faixa.");
        }

        return new PontoGeografico(latitude, longitude);
    }

    /// <summary>A distância em linha reta até outro ponto (fórmula de haversine), em km.</summary>
    /// <param name="outro">O outro ponto.</param>
    /// <returns>A distância.</returns>
    public double DistanciaKmAte(PontoGeografico outro)
    {
        ArgumentNullException.ThrowIfNull(outro);
        double dLat = Radianos(outro.Latitude - Latitude);
        double dLon = Radianos(outro.Longitude - Longitude);
        double a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(Radianos(Latitude)) * Math.Cos(Radianos(outro.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return 2 * RaioDaTerraKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double Radianos(double graus) => graus * Math.PI / 180;
}
