package br.com.fiap.sombra.clima.dominio;

/**
 * O índice de calor (sensação térmica) pela equação oficial da NWS/WPC: regressão de Rothfusz (1990, SR 90-23), os
 * dois ajustes e a fórmula simples de Steadman, na leitura adotada por todas as linguagens do Sombra
 * ({@code docs/contratos/niveis-de-risco.md}, ADR-006). Os 20 vetores do contrato rodam nos testes.
 *
 * @param celsius o índice em °C
 */
public record IndiceDeCalor(double celsius) {

    /** Calcula o índice a partir da temperatura do ar (°C) e da umidade relativa (%). */
    public static IndiceDeCalor calcular(double temperaturaC, double umidadePct) {
        double t = temperaturaC * 9.0 / 5.0 + 32.0;
        double simples = 0.5 * (t + 61.0 + (t - 68.0) * 1.2 + umidadePct * 0.094);
        double indiceF = (simples + t) / 2.0 < 80.0 ? simples : rothfuszAjustado(t, umidadePct);
        return new IndiceDeCalor((indiceF - 32.0) * 5.0 / 9.0);
    }

    /** O nível de risco deste índice. */
    public NivelDeRisco nivel() {
        return NivelDeRisco.classificar(celsius);
    }

    /** O índice com duas casas, como sai na API e no banco. */
    public double arredondado() {
        return Math.round(celsius * 100.0) / 100.0;
    }

    private static double rothfuszAjustado(double t, double ur) {
        double hi = -42.379 + 2.04901523 * t + 10.14333127 * ur - 0.22475541 * t * ur
                - 0.00683783 * t * t - 0.05481717 * ur * ur + 0.00122874 * t * t * ur
                + 0.00085282 * t * ur * ur - 0.00000199 * t * t * ur * ur;
        if (ur < 13.0 && t >= 80.0 && t <= 112.0) {
            hi -= ((13.0 - ur) / 4.0) * Math.sqrt((17.0 - Math.abs(t - 95.0)) / 17.0);
        } else if (ur > 85.0 && t >= 80.0 && t <= 87.0) {
            hi += ((ur - 85.0) / 10.0) * ((87.0 - t) / 5.0);
        }
        return hi;
    }
}
