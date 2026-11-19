package br.com.fiap.sombra.clima.dominio;

import java.util.Objects;

/**
 * A previsão das próximas 24 h para uma região.
 *
 * @param probabilidadePerigo    a probabilidade de o índice de calor chegar a {@code PERIGO}, de 0 a 1
 * @param nivelPrevisto          o nível previsto para o pico
 * @param indiceDeCalorPrevistoC o pico previsto do índice de calor, °C (o Core pode comparar com o limiar pessoal, R3)
 * @param horizonteHoras         o horizonte da previsão
 * @param modelo                 qual modelo previu (ex.: {@code xgb-calor-v1}, {@code regra-persistencia-v1})
 * @param fonte                  {@code onnx} (o modelo de ML) ou {@code regra} (o fallback)
 */
public record PrevisaoDeCalor(double probabilidadePerigo, NivelDeRisco nivelPrevisto, double indiceDeCalorPrevistoC,
        int horizonteHoras, String modelo, String fonte) {

    public PrevisaoDeCalor {
        if (Double.isNaN(probabilidadePerigo) || probabilidadePerigo < 0.0 || probabilidadePerigo > 1.0) {
            throw new IllegalArgumentException("probabilidade fora de 0..1");
        }
        Objects.requireNonNull(nivelPrevisto, "nivelPrevisto");
        Objects.requireNonNull(modelo, "modelo");
        Objects.requireNonNull(fonte, "fonte");
    }
}
