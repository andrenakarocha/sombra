package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;

/**
 * Um ponto na superfície da Terra (WGS 84). Recusa latitude fora de −90..90 e longitude fora de −180..180.
 *
 * @param latitude  em graus decimais
 * @param longitude em graus decimais
 */
@Embeddable
public record Coordenada(
        @Column(name = "latitude", nullable = false) double latitude,
        @Column(name = "longitude", nullable = false) double longitude) {

    /** Raio médio da Terra (IUGG), o mesmo do Core. */
    public static final double RAIO_DA_TERRA_KM = 6371.0088;

    public Coordenada {
        if (Double.isNaN(latitude) || latitude < -90.0 || latitude > 90.0
                || Double.isNaN(longitude) || longitude < -180.0 || longitude > 180.0) {
            throw new RegraViolada("COORDENADA_INVALIDA",
                    "A coordenada precisa ter latitude entre -90 e 90 e longitude entre -180 e 180.");
        }
    }

    /** A distância em km pela fórmula de haversine (círculo máximo). */
    public double distanciaKmAte(Coordenada outra) {
        double lat1 = Math.toRadians(latitude);
        double lat2 = Math.toRadians(outra.latitude);
        double dLat = lat2 - lat1;
        double dLon = Math.toRadians(outra.longitude - longitude);
        double a = Math.pow(Math.sin(dLat / 2), 2) + Math.cos(lat1) * Math.cos(lat2) * Math.pow(Math.sin(dLon / 2), 2);
        return 2 * RAIO_DA_TERRA_KM * Math.asin(Math.min(1.0, Math.sqrt(a)));
    }
}
