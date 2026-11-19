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

/** O endpoint que o Core consulta: status e corpo do risco no replay real, no ao vivo sem dado e numa região que não existe. */
@SpringBootTest
@AutoConfigureMockMvc
@Import({PostgresDeTeste.class, RelogioDeTeste.Configuracao.class})
class RiscoDeCalorApiTest {

    @Autowired
    MockMvc mvc;

    @Autowired
    RelogioDeTeste relogio;

    @AfterEach
    void desligar() throws Exception {
        mvc.perform(delete("/api/v1/replay"));
    }

    @Test
    void noPicoRealDaOndaDeCalorOAgoraEAPrevisaoSaemDoReplay() throws Exception {
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content("""
                {"regiao": "SP-MIRANTE", "inicio": "2023-11-15T18:00:00Z", "velocidade": 60}""")).andExpect(status().isOk());

        mvc.perform(get("/api/v1/regioes/SP-MIRANTE/risco-calor"))
                .andExpect(status().isOk())
                .andExpect(jsonPath("$.regiao").value("SP-MIRANTE"))
                .andExpect(jsonPath("$.fonteDados").value("replay-inmet"))
                .andExpect(jsonPath("$.agora.observadaEm").value("2023-11-15T18:00:00Z"))
                .andExpect(jsonPath("$.agora.temperaturaC").value(35.7))
                .andExpect(jsonPath("$.agora.indiceDeCalorC").value(35.47))
                .andExpect(jsonPath("$.agora.nivel").value("CAUTELA_EXTREMA"))
                .andExpect(jsonPath("$.previsao24h.indiceDeCalorPrevistoC").value(35.47))
                .andExpect(jsonPath("$.previsao24h.nivelPrevisto").value("CAUTELA_EXTREMA"))
                .andExpect(jsonPath("$.previsao24h.probabilidadePerigo").value(0.07))
                .andExpect(jsonPath("$.previsao24h.horizonteHoras").value(24))
                .andExpect(jsonPath("$.previsao24h.fonte").value("regra"))
                .andExpect(jsonPath("$.previsao24h.modelo").value("regra-persistencia-v1"));

        relogio.adiantar(Duration.ofMinutes(1));
        mvc.perform(get("/api/v1/regioes/SP-MIRANTE/risco-calor"))
                .andExpect(jsonPath("$.agora.observadaEm").value("2023-11-15T19:00:00Z"));
    }

    @Test
    void aoVivoSemObservacaoRecenteResponde503() throws Exception {
        mvc.perform(get("/api/v1/regioes/SP-MIRANTE/risco-calor"))
                .andExpect(status().isServiceUnavailable())
                .andExpect(jsonPath("$.title").value("Dado climático indisponível"))
                .andExpect(jsonPath("$.detail").value("A estação A701 da região SP-MIRANTE não tem observação nas últimas 3 horas."));
    }

    @Test
    void regiaoInexistenteResponde404() throws Exception {
        mvc.perform(get("/api/v1/regioes/SP-XX/risco-calor")).andExpect(status().isNotFound());
    }
}
