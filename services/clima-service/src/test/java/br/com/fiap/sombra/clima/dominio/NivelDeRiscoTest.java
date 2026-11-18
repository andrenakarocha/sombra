package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.CsvSource;

class NivelDeRiscoTest {

    @ParameterizedTest(name = "{0} °C → {1}")
    @CsvSource({
        "-5.0, NORMAL", "26.66, NORMAL", "26.67, ATENCAO", "32.21, ATENCAO", "32.22, CAUTELA_EXTREMA",
        "39.43, CAUTELA_EXTREMA", "39.44, PERIGO", "51.66, PERIGO", "51.67, PERIGO_EXTREMO", "70.0, PERIGO_EXTREMO"
    })
    void classificaComIntervaloFechadoEmbaixoEAbertoEmCima(double indiceC, NivelDeRisco esperado) {
        assertThat(NivelDeRisco.classificar(indiceC)).isEqualTo(esperado);
    }

    @Test
    void recusaIndiceIndefinido() {
        assertThatThrownBy(() -> NivelDeRisco.classificar(Double.NaN)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void comparaAGravidade() {
        assertThat(NivelDeRisco.PERIGO_EXTREMO.peloMenos(NivelDeRisco.PERIGO)).isTrue();
        assertThat(NivelDeRisco.PERIGO.peloMenos(NivelDeRisco.PERIGO)).isTrue();
        assertThat(NivelDeRisco.CAUTELA_EXTREMA.peloMenos(NivelDeRisco.PERIGO)).isFalse();
    }
}
