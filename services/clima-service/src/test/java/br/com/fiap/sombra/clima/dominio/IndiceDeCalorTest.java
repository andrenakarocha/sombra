package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.Arguments;
import org.junit.jupiter.params.provider.MethodSource;

/** Os 20 vetores do contrato ({@code docs/contratos/indice-de-calor.vetores.json}): o mesmo arquivo roda no C#, no C++ e no Python. */
class IndiceDeCalorTest {

    private static final Path VETORES = Path.of("../../docs/contratos/indice-de-calor.vetores.json");

    static JsonNode contrato() throws IOException {
        return new ObjectMapper().readTree(VETORES.toFile());
    }

    static Stream<Arguments> vetores() throws IOException {
        JsonNode contrato = contrato();
        List<Arguments> casos = new ArrayList<>();
        contrato.get("vetores").forEach(v -> casos.add(Arguments.of(
                v.get("caso").asText(),
                v.get("temperaturaC").asDouble(),
                v.get("umidadePct").asDouble(),
                v.get("indiceDeCalorC").asDouble(),
                NivelDeRisco.valueOf(v.get("nivel").asText()),
                contrato.get("toleranciaC").asDouble())));
        return casos.stream();
    }

    @ParameterizedTest(name = "{0}")
    @MethodSource("vetores")
    void bateComOVetorDoContrato(String caso, double t, double ur, double esperadoC, NivelDeRisco nivel, double tolerancia) {
        IndiceDeCalor indice = IndiceDeCalor.calcular(t, ur);

        assertThat(indice.celsius()).as(caso).isCloseTo(esperadoC, org.assertj.core.data.Offset.offset(tolerancia));
        assertThat(indice.nivel()).as(caso).isEqualTo(nivel);
    }

    @Test
    void osVetoresCobremOsCincoNiveisEOsLimiaresSaoOsDoContrato() throws IOException {
        JsonNode contrato = contrato();
        assertThat(vetores().map(a -> a.get()[4]).distinct()).containsExactlyInAnyOrder((Object[]) NivelDeRisco.values());
        contrato.get("limiaresC").properties().forEach(e ->
                assertThat(NivelDeRisco.valueOf(e.getKey()).limiarInferiorC()).isEqualTo(e.getValue().asDouble()));
    }

    @Test
    void arredondaComDuasCasas() {
        assertThat(new IndiceDeCalor(40.6849).arredondado()).isEqualTo(40.68);
        assertThat(new IndiceDeCalor(40.685).arredondado()).isEqualTo(40.69);
    }
}
