using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sombra.Dominio.Comum;
using Sombra.Dominio.Geo;

namespace Sombra.Dominio.Tests.Geo;

/// <summary>A distância de haversine que a tela "Perto de mim" usa.</summary>
[TestClass]
public sealed class PontoGeograficoTests
{
    /// <summary>A distância é simétrica, zero no mesmo ponto, e bate com um grau de latitude (≈ 111,2 km).</summary>
    [TestMethod]
    public void HaversineBateComOGrauDeLatitude()
    {
        PontoGeografico mirante = PontoGeografico.Criar(-23.4964, -46.6200);
        PontoGeografico umGrauAoSul = PontoGeografico.Criar(-24.4964, -46.6200);

        Assert.AreEqual(0.0, mirante.DistanciaKmAte(mirante), 1e-9);
        Assert.AreEqual(111.19, mirante.DistanciaKmAte(umGrauAoSul), 0.01);
        Assert.AreEqual(mirante.DistanciaKmAte(umGrauAoSul), umGrauAoSul.DistanciaKmAte(mirante), 1e-9);
    }

    /// <summary>Coordenada fora da Terra é recusada.</summary>
    /// <param name="latitude">A latitude.</param>
    /// <param name="longitude">A longitude.</param>
    [TestMethod]
    [DataRow(91.0, 0.0)]
    [DataRow(0.0, -181.0)]
    [DataRow(double.NaN, 0.0)]
    public void RecusaForaDaFaixa(double latitude, double longitude)
        => Assert.ThrowsExactly<RegraVioladaException>(() => PontoGeografico.Criar(latitude, longitude));
}
