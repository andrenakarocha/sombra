package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.within;

import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;
import org.junit.jupiter.api.Test;

class CoordenadaTest {

    @ParameterizedTest
    @CsvSource({"90.1, 0", "-90.1, 0", "0, 180.1", "0, -180.1", "NaN, 0", "0, NaN"})
    void recusaForaDoGlobo(double lat, double lon) {
        assertThatThrownBy(() -> new Coordenada(lat, lon))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("COORDENADA_INVALIDA"));
    }

    @Test
    void umGrauDeLongitudeNoEquadorTemCercaDe111Km() {
        assertThat(new Coordenada(0, 0).distanciaKmAte(new Coordenada(0, 1))).isCloseTo(111.195, within(0.001));
        assertThat(new Coordenada(-23.5, -46.6).distanciaKmAte(new Coordenada(-23.5, -46.6))).isZero();
    }
}
