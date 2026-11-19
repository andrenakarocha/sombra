package br.com.fiap.sombra.clima.api;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import br.com.fiap.sombra.clima.apoio.RelogioDeTeste;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

/**
 * O contrato vivo com o Core: as respostas reais que o Core consome, gravadas em {@code docs/contratos/amostras-clima/}.
 * Este teste falha se a resposta mudar e a amostra não; os testes do {@code ClimaClient} do Core leem as mesmas
 * amostras. {@code SOMBRA_ATUALIZAR_AMOSTRAS=1} as regrava.
 */
@SpringBootTest
@AutoConfigureMockMvc
@Import({PostgresDeTeste.class, RelogioDeTeste.Configuracao.class})
class AmostrasDoContratoTest {

    private static final Path PASTA = Path.of("../../docs/contratos/amostras-clima");

    @Autowired
    MockMvc mvc;

    @AfterEach
    void desligar() throws Exception {
        mvc.perform(delete("/api/v1/replay"));
    }

    private void conferir(String nome, String corpo) throws Exception {
        ObjectMapper json = new ObjectMapper();
        String formatado = json.writerWithDefaultPrettyPrinter().writeValueAsString(json.readTree(corpo)) + "\n";
        Path amostra = PASTA.resolve(nome);
        if ("1".equals(System.getenv("SOMBRA_ATUALIZAR_AMOSTRAS"))) {
            Files.createDirectories(PASTA);
            Files.writeString(amostra, formatado, StandardCharsets.UTF_8);
        }
        assertThat(amostra).as("Falta a amostra: rode make amostras-clima.").exists();
        assertThat(Files.readString(amostra, StandardCharsets.UTF_8))
                .as("A resposta mudou e a amostra não: rode make amostras-clima e os testes do ClimaClient do Core.")
                .isEqualTo(formatado);
    }

    @Test
    void riscoDeCalorNoPicoRealDaOndaDeCalor() throws Exception {
        mvc.perform(post("/api/v1/replay").contentType(MediaType.APPLICATION_JSON).content("""
                {"regiao": "SP-MIRANTE", "inicio": "2023-11-15T18:00:00Z", "velocidade": 60}""")).andExpect(status().isOk());

        conferir("risco-calor.json", mvc.perform(get("/api/v1/regioes/SP-MIRANTE/risco-calor"))
                .andExpect(status().isOk()).andReturn().getResponse().getContentAsString(StandardCharsets.UTF_8));
    }

    @Test
    void pontosDeResfriamentoPertoDaEstacao() throws Exception {
        conferir("pontos-proximos.json", mvc.perform(get("/api/v1/pontos-de-resfriamento/proximos")
                        .param("lat", "-23.4964").param("lon", "-46.62").param("raioKm", "3"))
                .andExpect(status().isOk()).andReturn().getResponse().getContentAsString(StandardCharsets.UTF_8));
    }
}
