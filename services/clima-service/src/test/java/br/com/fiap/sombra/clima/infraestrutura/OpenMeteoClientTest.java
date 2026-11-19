package br.com.fiap.sombra.clima.infraestrutura;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.within;

import br.com.fiap.sombra.clima.apoio.RelogioDeTeste;
import br.com.fiap.sombra.clima.dominio.Coordenada;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.springframework.web.client.RestClient;

/** O cliente contra um servidor HTTP de verdade: conversão de unidades, horas faltando, horas futuras e as tentativas. */
class OpenMeteoClientTest {

    final OpenMeteoFalsa falsa = new OpenMeteoFalsa();
    final RelogioDeTeste relogio = new RelogioDeTeste(Instant.parse("2026-11-18T18:40:00Z"));
    final OpenMeteoClient cliente = new OpenMeteoClient(
            new ConfiguracaoOpenMeteo(true, falsa.url(), Duration.ofMinutes(10), Duration.ofSeconds(2), 3, Duration.ofMillis(1)),
            RestClient.builder(), relogio);

    @AfterEach
    void parar() {
        falsa.close();
    }

    @Test
    void trazAsHorasJaMedidasNasUnidadesDoDominio() {
        List<HoraMedida> horas = cliente.ultimasHoras(new Coordenada(-23.4964, -46.62), 24);

        // 18h tem temperatura nula (pulada) e 19h ainda não aconteceu às 18h40
        assertThat(horas).extracting(HoraMedida::observadaEm)
                .containsExactly(Instant.parse("2026-11-18T16:00:00Z"), Instant.parse("2026-11-18T17:00:00Z"));
        var ultima = horas.getLast().medicao();
        assertThat(ultima.temperaturaC()).isEqualTo(35.0);
        assertThat(ultima.umidadePct()).isEqualTo(50.0);
        assertThat(ultima.radiacaoKjM2()).isCloseTo(1800.0, within(1e-9));
        assertThat(ultima.indiceDeCalor().arredondado()).isEqualTo(40.68);
        assertThat(falsa.ultimaConsulta()).contains("latitude=-23.4964", "past_hours=24", "timezone=GMT",
                "wind_speed_unit=ms", "relative_humidity_2m");
    }

    @Test
    void tentaDeNovoQuandoAOpenMeteoFalhaEDepoisResponde() {
        falsa.responder(503, "{}").responder(500, "{}");

        assertThat(cliente.ultimasHoras(new Coordenada(-23.4964, -46.62), 24)).hasSize(2);
        assertThat(falsa.chamadas()).isEqualTo(3);
    }

    @Test
    void desisteDepoisDasTentativasComFonteIndisponivel() {
        falsa.responder(503, "{}").responder(503, "{}").responder(503, "{}");

        assertThatThrownBy(() -> cliente.ultimasHoras(new Coordenada(-23.4964, -46.62), 24))
                .isInstanceOf(FonteIndisponivel.class);
        assertThat(falsa.chamadas()).isEqualTo(3);
    }

    @Test
    void respostaSemSerieHorariaContaComoFalha() {
        falsa.responder(200, "{}").responder(200, "{}").responder(200, "{}");

        assertThatThrownBy(() -> cliente.ultimasHoras(new Coordenada(-23.4964, -46.62), 24))
                .isInstanceOf(FonteIndisponivel.class);
    }

    @Test
    void horaComValorForaDaFaixaEPuladaSemDerrubarAsOutras() {
        falsa.responder(200, OpenMeteoFalsa.DUAS_HORAS.replace("[40, 50, 55, 60]", "[140, 50, 55, 60]"));

        assertThat(cliente.ultimasHoras(new Coordenada(-23.4964, -46.62), 24)).hasSize(1);
    }
}
