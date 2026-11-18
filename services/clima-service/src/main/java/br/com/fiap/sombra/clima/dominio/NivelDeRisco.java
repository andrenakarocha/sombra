package br.com.fiap.sombra.clima.dominio;

/**
 * Os cinco níveis de risco de calor, pelas faixas da tabela da NWS em °C (contrato {@code docs/contratos/niveis-de-risco.md}).
 * Intervalo fechado embaixo e aberto em cima.
 */
public enum NivelDeRisco {
    NORMAL(Double.NEGATIVE_INFINITY),
    ATENCAO(26.67),
    CAUTELA_EXTREMA(32.22),
    PERIGO(39.44),
    PERIGO_EXTREMO(51.67);

    private final double limiarInferiorC;

    NivelDeRisco(double limiarInferiorC) {
        this.limiarInferiorC = limiarInferiorC;
    }

    /** O menor índice de calor (°C) que já cai neste nível. */
    public double limiarInferiorC() {
        return limiarInferiorC;
    }

    /** Classifica um índice de calor em °C. */
    public static NivelDeRisco classificar(double indiceDeCalorC) {
        if (Double.isNaN(indiceDeCalorC)) {
            throw new IllegalArgumentException("índice de calor indefinido");
        }
        NivelDeRisco[] niveis = values();
        for (int i = niveis.length - 1; i > 0; i--) {
            if (indiceDeCalorC >= niveis[i].limiarInferiorC) {
                return niveis[i];
            }
        }
        return NORMAL;
    }

    /** Se este nível é pelo menos tão grave quanto o outro. */
    public boolean peloMenos(NivelDeRisco outro) {
        return compareTo(outro) >= 0;
    }
}
