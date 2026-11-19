package br.com.fiap.sombra.clima.api;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import br.com.fiap.sombra.clima.apoio.RelogioDeTeste;
import java.time.Duration;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

/** O replay da onda de calor real de nov/2023 avança hora a hora no relógio acelerado e encerra sozinho no fim dos dados. */
@SpringBootTest
@AutoConfigureMockMvc
@Import({PostgresDeTeste.class, RelogioDeTeste.Configuracao.class})
class ReplayApiTest {

    @Autowired
    MockMvc mvc;

    @Autowired
    RelogioDeTeste relogio;

    @AfterEach
    void desligar() throws Exception {
        mvc.perform(delete("/api/v1/replay")).andExpect(status().isNoContent());
    }

    private void ligar(String json) throws Exception {
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content(json))
                .andExpect(status().isOk());
    }

    @Test
    void avancaUmaHoraDeDadoPorMinutoNaVelocidadeSessenta() throws Exception {
        ligar("""
                {"regiao": "SP-MIRANTE", "inicio": "2023-11-12T00:00:00-03:00", "velocidade": 60}""");

        mvc.perform(get("/api/v1/replay"))
                .andExpect(jsonPath("$.ativo").value(true))
                .andExpect(jsonPath("$.instanteAtual").value("2023-11-12T03:00:00Z"))
                .andExpect(jsonPath("$.fim").value("2023-11-30T23:00:00Z"));
        relogio.adiantar(Duration.ofMinutes(1));
        mvc.perform(get("/api/v1/replay")).andExpect(jsonPath("$.instanteAtual").value("2023-11-12T04:00:00Z"));
        relogio.adiantar(Duration.ofMinutes(2));
        mvc.perform(get("/api/v1/replay")).andExpect(jsonPath("$.instanteAtual").value("2023-11-12T06:00:00Z"));
    }

    @Test
    void encerraSozinhoQuandoPassaDaUltimaHoraComDado() throws Exception {
        ligar("""
                {"regiao": "SP-MIRANTE", "inicio": "2023-11-30T00:00:00Z", "velocidade": 3600}""");

        relogio.adiantar(Duration.ofMinutes(1));

        mvc.perform(get("/api/v1/replay")).andExpect(jsonPath("$.ativo").value(false));
    }

    @Test
    void desligadoRespondeAtivoFalso() throws Exception {
        mvc.perform(get("/api/v1/replay"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.ativo").value(false))
                .andExpect(jsonPath("$.regiao").doesNotExist());
    }

    @Test
    void semDadoDoInmetNoInicioDa422() throws Exception {
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content("""
                        {"regiao": "SP-MIRANTE", "inicio": "2025-01-01T00:00:00Z", "velocidade": 60}"""))
                .andExpect(status().isUnprocessableEntity())
                .andExpect(jsonPath("$.regra").value("REPLAY_SEM_DADOS"));
    }

    @Test
    void regiaoInexistenteDa404EVelocidadeForaDoLimiteDa400() throws Exception {
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content("""
                        {"regiao": "SP-XX", "inicio": "2023-11-12T00:00:00Z", "velocidade": 60}"""))
                .andExpect(status().isNotFound());
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content("""
                        {"regiao": "SP-MIRANTE", "inicio": "2023-11-12T00:00:00Z", "velocidade": 0}"""))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.errors.velocidade").isArray());
    }
}
