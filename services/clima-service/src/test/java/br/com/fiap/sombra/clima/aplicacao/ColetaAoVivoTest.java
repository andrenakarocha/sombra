package br.com.fiap.sombra.clima.aplicacao;

import static org.assertj.core.api.Assertions.assertThat;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import br.com.fiap.sombra.clima.apoio.RelogioDeTeste;
import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.infraestrutura.ObservacaoRepository;
import br.com.fiap.sombra.clima.infraestrutura.OpenMeteoFalsa;
import java.time.Instant;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.test.context.DynamicPropertyRegistry;
import org.springframework.test.context.DynamicPropertySource;

/** A coleta grava as horas novas da Open-Meteo, não duplica na segunda volta e sobrevive à queda da fonte. */
@SpringBootTest
@Import({PostgresDeTeste.class, RelogioDeTeste.Configuracao.class})
class ColetaAoVivoTest {

    static final OpenMeteoFalsa FALSA = new OpenMeteoFalsa();

    @DynamicPropertySource
    static void apontarParaAFalsa(DynamicPropertyRegistry registro) {
        registro.add("clima.open-meteo.url", () -> FALSA.url().toString());
        registro.add("clima.open-meteo.espera", () -> "PT0.001S");
    }

    @AfterAll
    static void parar() {
        FALSA.close();
    }

    @Autowired
    ColetaAoVivo coleta;

    @Autowired
    ObservacaoRepository observacoes;

    @Autowired
    RelogioDeTeste relogio;

    @Test
    void gravaAsHorasNovasUmaVezSoEAguentaAQuedaDaFonte() {
        relogio.ajustar(Instant.parse("2026-11-18T18:40:00Z"));

        assertThat(coleta.coletar()).isEqualTo(2);
        assertThat(coleta.coletar()).isZero();
        assertThat(observacoes.findFirstByEstacaoCodigoAndFonteOrderByObservadaEmDesc("A701", FonteDaObservacao.OPEN_METEO))
                .hasValueSatisfying(o -> {
                    assertThat(o.getObservadaEm()).isEqualTo(Instant.parse("2026-11-18T17:00:00Z"));
                    assertThat(o.getIndiceDeCalorC()).isEqualTo(40.68);
                });

        FALSA.responder(503, "{}").responder(503, "{}").responder(503, "{}");
        assertThat(coleta.coletar()).isZero();
    }
}
