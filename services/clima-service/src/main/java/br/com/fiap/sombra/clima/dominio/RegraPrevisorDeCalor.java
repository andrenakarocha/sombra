package br.com.fiap.sombra.clima.dominio;

import java.util.List;

/**
 * O fallback por regra: persistência do pico. O pico das próximas 24 h é o pico do índice de calor das últimas 24 h (o
 * mesmo baseline de persistência que o notebook de ML compara com o XGBoost), e a probabilidade de perigo é uma
 * logística centrada no limiar de {@code PERIGO} (39,44 °C), com escala de 1,5 °C: 0,5 no limiar, ≈ 0,07 três graus
 * abaixo e ≈ 0,93 quatro graus acima.
 */
public final class RegraPrevisorDeCalor implements PrevisorDeCalor {

    public static final String MODELO = "regra-persistencia-v1";
    static final double ESCALA_C = 1.5;

    @Override
    public PrevisaoDeCalor prever(List<Observacao> janela) {
        if (janela == null || janela.isEmpty()) {
            throw new IllegalArgumentException("a janela precisa de pelo menos uma observação");
        }
        double pico = janela.stream().mapToDouble(Observacao::getIndiceDeCalorC).max().orElseThrow();
        double z = (pico - NivelDeRisco.PERIGO.limiarInferiorC()) / ESCALA_C;
        double probabilidade = Math.round(1.0 / (1.0 + Math.exp(-z)) * 100.0) / 100.0;
        return new PrevisaoDeCalor(probabilidade, NivelDeRisco.classificar(pico), Math.round(pico * 100.0) / 100.0, 24,
                MODELO, "regra");
    }
}
