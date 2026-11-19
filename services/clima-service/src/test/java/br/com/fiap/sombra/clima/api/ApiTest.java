package br.com.fiap.sombra.clima.api;

import static org.hamcrest.Matchers.hasSize;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.put;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.header;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import org.junit.jupiter.api.Nested;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.ResultActions;
import org.springframework.transaction.annotation.Transactional;

/**
 * A API de fora, como o Core a vê: status e corpo de cada endpoint, contra o PostgreSQL real com o seed das migrações.
 * Cada teste roda numa transação desfeita no fim, então um não vê o que o outro gravou.
 */
@SpringBootTest
@AutoConfigureMockMvc
@Import(PostgresDeTeste.class)
@Transactional
class ApiTest {

    @Autowired
    MockMvc mvc;

    ResultActions enviar(org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder req, String json)
            throws Exception {
        return mvc.perform(req.contentType(MediaType.APPLICATION_JSON).content(json));
    }

    @Nested
    class Regioes {

        @Test
        void listaPaginadaComAEstacaoDeReferencia() throws Exception {
            mvc.perform(get("/api/v1/regioes"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.pagina").value(1))
                    .andExpect(jsonPath("$.tamanho").value(20))
                    .andExpect(jsonPath("$.total").value(1))
                    .andExpect(jsonPath("$.itens[0].codigo").value("SP-MIRANTE"))
                    .andExpect(jsonPath("$.itens[0].estacao.codigo").value("A701"));
        }

        @Test
        void consultaPeloCodigoEmQualquerCaixa() throws Exception {
            mvc.perform(get("/api/v1/regioes/sp-mirante"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.nome").value("Zona Norte de São Paulo (Mirante de Santana)"));
        }

        @Test
        void regiaoInexistenteDa404() throws Exception {
            mvc.perform(get("/api/v1/regioes/SP-XX"))
                    .andExpect(status().isNotFound())
                    .andExpect(jsonPath("$.title").value("Recurso não encontrado"));
        }

        @Test
        void criaEditaERemove() throws Exception {
            enviar(post("/api/v1/regioes"), """
                    {"codigo": "sp-santana", "nome": "Santana", "estacao": "a701"}""")
                    .andExpect(status().isCreated())
                    .andExpect(jsonPath("$.codigo").value("SP-SANTANA"))
                    .andExpect(jsonPath("$.estacao.codigo").value("A701"));

            enviar(put("/api/v1/regioes/SP-SANTANA"), """
                    {"nome": "Santana e Tucuruvi", "estacao": "A701"}""")
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.nome").value("Santana e Tucuruvi"));

            mvc.perform(delete("/api/v1/regioes/SP-SANTANA")).andExpect(status().isNoContent());
            mvc.perform(get("/api/v1/regioes/SP-SANTANA")).andExpect(status().isNotFound());
        }

        @Test
        void codigoRepetidoDa409() throws Exception {
            enviar(post("/api/v1/regioes"), """
                    {"codigo": "SP-MIRANTE", "nome": "De novo", "estacao": "A701"}""")
                    .andExpect(status().isConflict());
        }

        @Test
        void regiaoSemEstacaoValidaDa422ComARegra() throws Exception {
            enviar(post("/api/v1/regioes"), """
                    {"codigo": "SP-SANTANA", "nome": "Santana", "estacao": "Z999"}""")
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("REGIAO_SEM_ESTACAO"));
        }

        @Test
        void campoEmBrancoDa400ComOErroDoCampoEmPortugues() throws Exception {
            enviar(post("/api/v1/regioes"), """
                    {"codigo": "SP-SANTANA", "nome": " ", "estacao": "A701"}""")
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.errors.nome[0]").value("não deve estar em branco"));
        }

        @Test
        void tamanhoDePaginaAcimaDoLimiteDa400() throws Exception {
            mvc.perform(get("/api/v1/regioes?tamanho=101"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.errors.tamanho").isArray());
        }
    }

    @Nested
    class Observacoes {

        static final String HORA_NOVA = """
                {"observadaEm": "2026-11-18T18:00:00Z", "temperaturaC": 35.0, "umidadePct": 50.0, "ventoMs": 1.2}""";

        @Test
        void registraComOIndiceEONivelCalculadosPeloClima() throws Exception {
            enviar(post("/api/v1/estacoes/A701/observacoes"), HORA_NOVA)
                    .andExpect(status().isCreated())
                    .andExpect(jsonPath("$.estacao").value("A701"))
                    .andExpect(jsonPath("$.indiceDeCalorC").value(40.68))
                    .andExpect(jsonPath("$.nivel").value("PERIGO"))
                    .andExpect(jsonPath("$.fonte").value("INMET"))
                    .andExpect(jsonPath("$.pressaoHpa").doesNotExist());
        }

        @Test
        void menosNoveMilDoInmetDa422() throws Exception {
            enviar(post("/api/v1/estacoes/A701/observacoes"), """
                    {"observadaEm": "2026-11-18T18:00:00Z", "temperaturaC": -9999, "umidadePct": 50}""")
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("OBSERVACAO_FALTANTE"));
        }

        @Test
        void umidadeForaDaFaixaDa422() throws Exception {
            enviar(post("/api/v1/estacoes/A701/observacoes"), """
                    {"observadaEm": "2026-11-18T18:00:00Z", "temperaturaC": 30, "umidadePct": 120}""")
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("UMIDADE_FORA_DA_FAIXA"));
        }

        @Test
        void mesmaHoraDuasVezesDa409() throws Exception {
            enviar(post("/api/v1/estacoes/A701/observacoes"), """
                    {"observadaEm": "2023-11-15T18:00:00Z", "temperaturaC": 30, "umidadePct": 50}""")
                    .andExpect(status().isConflict());
        }

        @Test
        void estacaoInexistenteDa404() throws Exception {
            enviar(post("/api/v1/estacoes/Z999/observacoes"), HORA_NOVA).andExpect(status().isNotFound());
        }

        @Test
        void semTemperaturaDa400() throws Exception {
            enviar(post("/api/v1/estacoes/A701/observacoes"), """
                    {"observadaEm": "2026-11-18T18:00:00Z", "umidadePct": 50}""")
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.errors.temperaturaC").isArray());
        }

        @Test
        void consultaODiaMaisQuenteDaOndaDeCalorPaginado() throws Exception {
            mvc.perform(get("/api/v1/estacoes/A701/observacoes")
                            .param("de", "2023-11-15T00:00:00Z").param("ate", "2023-11-16T00:00:00Z")
                            .param("tamanho", "5"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.total").value(24))
                    .andExpect(jsonPath("$.itens", hasSize(5)))
                    .andExpect(jsonPath("$.itens[0].observadaEm").value("2023-11-15T23:00:00Z"));
        }

        @Test
        void periodoInvertidoDa422() throws Exception {
            mvc.perform(get("/api/v1/estacoes/A701/observacoes")
                            .param("de", "2023-11-16T00:00:00Z").param("ate", "2023-11-15T00:00:00Z"))
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("PERIODO_INVALIDO"));
        }

        @Test
        void dataMalFormadaDa400() throws Exception {
            mvc.perform(get("/api/v1/estacoes/A701/observacoes").param("de", "ontem").param("ate", "hoje"))
                    .andExpect(status().isBadRequest())
                    .andExpect(jsonPath("$.detail").value("O parâmetro 'de' tem um valor inválido."));
        }
    }

    @Nested
    class Pontos {

        static final String BIBLIOTECA = """
                {"nome": "Biblioteca de teste", "tipo": "BIBLIOTECA", "latitude": -23.5, "longitude": -46.62,
                 "horario": {"abre": "09:00", "fecha": "18:00"}, "acessivel": true, "fonte": "teste"}""";

        @Test
        void osMaisPertoDoMiranteVemPrimeiroComADistancia() throws Exception {
            mvc.perform(get("/api/v1/pontos-de-resfriamento/proximos")
                            .param("lat", "-23.4964").param("lon", "-46.62").param("raioKm", "1"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$", hasSize(2)))
                    .andExpect(jsonPath("$[0].nome").value("Biblioteca Municipal Nuto Sant'anna"))
                    .andExpect(jsonPath("$[0].distanciaKm").value(0.2))
                    .andExpect(jsonPath("$[0].horario.abre").value("09:00"))
                    .andExpect(jsonPath("$[0].fonte").value(org.hamcrest.Matchers.startsWith("OpenStreetMap")))
                    .andExpect(jsonPath("$[1].nome").value("Santana Shopping"));
        }

        @Test
        void raioForaDoLimiteDa422() throws Exception {
            mvc.perform(get("/api/v1/pontos-de-resfriamento/proximos")
                            .param("lat", "-23.4964").param("lon", "-46.62").param("raioKm", "50"))
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("RAIO_INVALIDO"));
        }

        @Test
        void criaConsultaEditaERemove() throws Exception {
            String local = enviar(post("/api/v1/pontos-de-resfriamento"), BIBLIOTECA)
                    .andExpect(status().isCreated())
                    .andExpect(jsonPath("$.id").isNotEmpty())
                    .andExpect(jsonPath("$.horario.fecha").value("18:00"))
                    .andReturn().getResponse().getContentAsString();
            String id = com.jayway.jsonpath.JsonPath.read(local, "$.id");

            mvc.perform(get("/api/v1/pontos-de-resfriamento/" + id))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.nome").value("Biblioteca de teste"));
            enviar(put("/api/v1/pontos-de-resfriamento/" + id), BIBLIOTECA.replace("Biblioteca de teste", "UBS de teste")
                    .replace("BIBLIOTECA", "UBS"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.tipo").value("UBS"));
            mvc.perform(delete("/api/v1/pontos-de-resfriamento/" + id)).andExpect(status().isNoContent());
            mvc.perform(get("/api/v1/pontos-de-resfriamento/" + id)).andExpect(status().isNotFound());
        }

        @Test
        void fechaAntesDeAbrirDa422() throws Exception {
            enviar(post("/api/v1/pontos-de-resfriamento"), BIBLIOTECA.replace("\"09:00\"", "\"19:00\""))
                    .andExpect(status().isUnprocessableEntity())
                    .andExpect(jsonPath("$.regra").value("HORARIO_INVALIDO"));
        }

        @Test
        void tipoDesconhecidoDa400() throws Exception {
            enviar(post("/api/v1/pontos-de-resfriamento"), BIBLIOTECA.replace("\"BIBLIOTECA\"", "\"PISCINA\""))
                    .andExpect(status().isBadRequest());
        }

        @Test
        void idQueNaoEUuidDa400() throws Exception {
            mvc.perform(get("/api/v1/pontos-de-resfriamento/123")).andExpect(status().isBadRequest());
        }

        @Test
        void listaPaginada() throws Exception {
            mvc.perform(get("/api/v1/pontos-de-resfriamento?tamanho=3"))
                    .andExpect(status().isOk())
                    .andExpect(jsonPath("$.total").value(9))
                    .andExpect(jsonPath("$.itens", hasSize(3)))
                    .andExpect(header().string("Content-Type", org.hamcrest.Matchers.containsString("application/json")));
        }
    }
}
