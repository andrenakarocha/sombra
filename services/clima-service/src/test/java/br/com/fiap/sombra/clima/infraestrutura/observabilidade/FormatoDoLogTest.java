package br.com.fiap.sombra.clima.infraestrutura.observabilidade;

import static org.assertj.core.api.Assertions.assertThat;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.LoggerContext;
import ch.qos.logback.classic.spi.LoggingEvent;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.Map;
import org.junit.jupiter.api.Test;
import org.slf4j.event.KeyValuePair;
import org.springframework.mock.env.MockEnvironment;

/** Uma linha JSON por evento, com os oito campos obrigatórios da observabilidade §1 e os campos do evento. */
class FormatoDoLogTest {

    final FormatoDoLog formato = new FormatoDoLog(new MockEnvironment()
            .withProperty("info.commit", "9571ead").withProperty("sombra.ambiente", "Demo"));

    LoggingEvent evento(Level nivel, String mensagem, Throwable erro) {
        LoggerContext contexto = new LoggerContext();
        LoggingEvent e = new LoggingEvent("x", contexto.getLogger("teste"), nivel, mensagem, erro, null);
        e.setTimeStamp(1763489702481L);
        e.setMDCPropertyMap(Map.of("correlationId", "snt-7f3a9c:57"));
        return e;
    }

    @Test
    void geraOsOitoCamposEOsDoEventoNumaLinhaSo() throws Exception {
        LoggingEvent e = evento(Level.INFO, "Observação registrada", null);
        e.addKeyValuePair(new KeyValuePair("event", "observacao.registrada"));
        e.addKeyValuePair(new KeyValuePair("estacao", "A701"));
        e.addKeyValuePair(new KeyValuePair("duracaoMs", 12L));

        String linha = formato.format(e);
        JsonNode json = new ObjectMapper().readTree(linha);

        assertThat(linha).endsWith("\n").doesNotContain("\n{");
        assertThat(json.get("timestamp").asText()).isEqualTo("2025-11-18T18:15:02.481Z");
        assertThat(json.get("level").asText()).isEqualTo("Information");
        assertThat(json.get("service").asText()).isEqualTo("clima-service");
        assertThat(json.get("version").asText()).isEqualTo("9571ead");
        assertThat(json.get("environment").asText()).isEqualTo("Demo");
        assertThat(json.get("correlationId").asText()).isEqualTo("snt-7f3a9c:57");
        assertThat(json.get("event").asText()).isEqualTo("observacao.registrada");
        assertThat(json.get("message").asText()).isEqualTo("Observação registrada");
        assertThat(json.get("estacao").asText()).isEqualTo("A701");
        assertThat(json.get("duracaoMs").isNumber()).isTrue();
    }

    @Test
    void semEventoNomeadoOCampoEventValeLog() throws Exception {
        JsonNode json = new ObjectMapper().readTree(formato.format(evento(Level.WARN, "aviso do framework", null)));

        assertThat(json.get("event").asText()).isEqualTo("log");
        assertThat(json.get("level").asText()).isEqualTo("Warning");
    }

    @Test
    void aExcecaoVaiNoCampoExceptionComOTipo() throws Exception {
        JsonNode json = new ObjectMapper().readTree(formato.format(evento(Level.ERROR, "falhou", new IllegalStateException("x"))));

        assertThat(json.get("level").asText()).isEqualTo("Error");
        assertThat(json.get("exception").asText()).startsWith("java.lang.IllegalStateException");
    }

    @Test
    void mapeiaOsNiveisDoLogbackParaOsDoSombra() {
        assertThat(FormatoDoLog.nivel(Level.DEBUG)).isEqualTo("Debug");
        assertThat(FormatoDoLog.nivel(Level.TRACE)).isEqualTo("Debug");
    }

    @Test
    void aUrlDoBancoSaiSemCredencial() {
        assertThat(CicloDeVida.semCredencial("jdbc:postgresql://u:s@db:5432/sombra_clima?password=x"))
                .isEqualTo("jdbc:postgresql://db:5432/sombra_clima");
    }
}
