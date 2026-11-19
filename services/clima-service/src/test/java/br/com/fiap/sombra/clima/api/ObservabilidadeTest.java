package br.com.fiap.sombra.clima.api;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.boot.test.system.CapturedOutput;
import org.springframework.boot.test.system.OutputCaptureExtension;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.transaction.annotation.Transactional;

/** Correlação ponta a ponta, log sem corpo nem dado sensível e as sondas de vivo, pronto e versão (observabilidade §3, §6, §7). */
@SpringBootTest
@AutoConfigureMockMvc
@Import(PostgresDeTeste.class)
@ExtendWith(OutputCaptureExtension.class)
@Transactional
class ObservabilidadeTest {

    @Autowired
    MockMvc mvc;

    static List<JsonNode> linhas(CapturedOutput saida) throws Exception {
        List<JsonNode> json = new ArrayList<>();
        for (String linha : saida.getOut().split("\n")) {
            if (linha.startsWith("{")) {
                json.add(new ObjectMapper().readTree(linha));
            }
        }
        return json;
    }

    @Test
    void aceitaOIdDoCoreDevolveNoHeaderENoErroEEscreveNoLog(CapturedOutput saida) throws Exception {
        mvc.perform(get("/api/v1/regioes/SP-XX").header("X-Correlation-Id", "core-4f1c2b7e9a0d4e6f"))
                .andExpect(status().isNotFound())
                .andExpect(header().string("X-Correlation-Id", "core-4f1c2b7e9a0d4e6f"))
                .andExpect(jsonPath("$.correlationId").value("core-4f1c2b7e9a0d4e6f"));

        assertThat(linhas(saida)).anySatisfy(l -> {
            assertThat(l.get("event").asText()).isEqualTo("requisicao.concluida");
            assertThat(l.get("correlationId").asText()).isEqualTo("core-4f1c2b7e9a0d4e6f");
            assertThat(l.get("rota").asText()).isEqualTo("/api/v1/regioes/{codigo}");
            assertThat(l.get("status").asInt()).isEqualTo(404);
            assertThat(l.get("environment").asText()).isEqualTo("Test");
        });
    }

    @Test
    void idForaDoFormatoEDescartadoEUmNovoEGerado() throws Exception {
        String malicioso = "x\" ,\"level\":\"Critical";
        String devolvido = mvc.perform(get("/api/v1/regioes").header("X-Correlation-Id", malicioso))
                .andExpect(status().isOk())
                .andReturn().getResponse().getHeader("X-Correlation-Id");

        assertThat(devolvido).matches("[0-9a-f]{16}");
        String semHeader = mvc.perform(get("/api/v1/regioes")).andReturn().getResponse().getHeader("X-Correlation-Id");
        assertThat(semHeader).matches("[0-9a-f]{16}").isNotEqualTo(devolvido);
    }

    @Test
    void oCorpoDaRequisicaoNuncaVaiParaOLog(CapturedOutput saida) throws Exception {
        mvc.perform(post("/api/v1/regioes").contentType(MediaType.APPLICATION_JSON).content("""
                {"codigo": "SP-SANTANA", "nome": "corpo-que-nao-pode-vazar", "estacao": "A701"}"""))
                .andExpect(status().isCreated());

        assertThat(saida.getOut()).doesNotContain("corpo-que-nao-pode-vazar").contains("requisicao.concluida");
    }

    @Test
    void sondasDeVivoProntoEVersao() throws Exception {
        mvc.perform(get("/actuator/health/liveness")).andExpect(status().isOk()).andExpect(jsonPath("$.status").value("UP"));
        mvc.perform(get("/actuator/health/readiness"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.components.db.status").value("UP"))
                .andExpect(jsonPath("$.components.previsor.details.fonte").value("regra"));
        mvc.perform(get("/actuator/info"))
                .andExpect(jsonPath("$.servico").value("clima-service"))
                .andExpect(jsonPath("$.commit").exists());
    }
}
