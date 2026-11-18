package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;

/**
 * O que a estação mediu numa hora. A temperatura e a umidade são obrigatórias (o índice de calor depende delas); as
 * outras grandezas podem faltar. Regras: {@code -9999} é o código de dado faltante do INMET e nunca é medição, a
 * umidade fica entre 0 e 100 % e a temperatura numa faixa física plausível.
 *
 * @param temperaturaC   temperatura do ar (bulbo seco), °C
 * @param umidadePct     umidade relativa do ar, %
 * @param pressaoHpa     pressão atmosférica ao nível da estação, hPa (opcional)
 * @param radiacaoKjM2   radiação global na hora, kJ/m² (opcional)
 * @param ventoMs        velocidade do vento, m/s (opcional)
 * @param precipitacaoMm precipitação na hora, mm (opcional)
 */
@Embeddable
public record Medicao(
        @Column(name = "temperatura_c", nullable = false) double temperaturaC,
        @Column(name = "umidade_pct", nullable = false) double umidadePct,
        @Column(name = "pressao_hpa") Double pressaoHpa,
        @Column(name = "radiacao_kj_m2") Double radiacaoKjM2,
        @Column(name = "vento_ms") Double ventoMs,
        @Column(name = "precipitacao_mm") Double precipitacaoMm) {

    /** O código que o INMET grava quando o sensor ou a transmissão falha. */
    public static final double DADO_FALTANTE_INMET = -9999.0;

    static final double TEMPERATURA_MINIMA_C = -40.0;
    static final double TEMPERATURA_MAXIMA_C = 60.0;

    public Medicao {
        recusarDadoFaltante(temperaturaC, umidadePct, pressaoHpa, radiacaoKjM2, ventoMs, precipitacaoMm);
        if (Double.isNaN(temperaturaC) || temperaturaC < TEMPERATURA_MINIMA_C || temperaturaC > TEMPERATURA_MAXIMA_C) {
            throw new RegraViolada("TEMPERATURA_FORA_DA_FAIXA", "A temperatura precisa estar entre -40 °C e 60 °C.");
        }
        if (Double.isNaN(umidadePct) || umidadePct < 0.0 || umidadePct > 100.0) {
            throw new RegraViolada("UMIDADE_FORA_DA_FAIXA", "A umidade relativa precisa estar entre 0 % e 100 %.");
        }
        if (negativo(ventoMs) || negativo(precipitacaoMm)) {
            throw new RegraViolada("GRANDEZA_NEGATIVA", "Vento e precipitação não podem ser negativos.");
        }
    }

    /** Só a temperatura e a umidade, sem as grandezas opcionais. */
    public static Medicao de(double temperaturaC, double umidadePct) {
        return new Medicao(temperaturaC, umidadePct, null, null, null, null);
    }

    /** O índice de calor desta medição. */
    public IndiceDeCalor indiceDeCalor() {
        return IndiceDeCalor.calcular(temperaturaC, umidadePct);
    }

    private static void recusarDadoFaltante(double temperaturaC, double umidadePct, Double... opcionais) {
        boolean faltante = temperaturaC == DADO_FALTANTE_INMET || umidadePct == DADO_FALTANTE_INMET;
        for (Double valor : opcionais) {
            faltante |= valor != null && valor == DADO_FALTANTE_INMET;
        }
        if (faltante) {
            throw new RegraViolada("OBSERVACAO_FALTANTE",
                    "O valor -9999 é o código de dado faltante do INMET: não é uma observação.");
        }
    }

    private static boolean negativo(Double valor) {
        return valor != null && valor < 0.0;
    }
}
