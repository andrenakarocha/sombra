package br.com.fiap.sombra.clima.api;

import static org.assertj.core.api.Assertions.assertThat;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.context.annotation.Import;
import org.springframework.test.web.servlet.MockMvc;

/**
 * A documentação é contrato: todo endpoint tem resumo, tag e os erros documentados, e o OpenAPI servido é igual ao
 * versionado em {@code docs/contratos/clima-service.openapi.json} ({@code make openapi-clima} o regenera).
 */
@SpringBootTest
@AutoConfigureMockMvc
@Import(PostgresDeTeste.class)
class ContratoOpenApiTest {

    private static final Path CONTRATO = Path.of("../../docs/contratos/clima-service.openapi.json");

    @Autowired
    MockMvc mvc;

    @Test
    void todoEndpointTemResumoTagEErrosEOContratoVersionadoEstaEmDia() throws Exception {
        String servido = mvc.perform(get("/v3/api-docs")).andExpect(status().isOk())
                .andReturn().getResponse().getContentAsString(StandardCharsets.UTF_8);
        ObjectMapper json = new ObjectMapper().enable(SerializationFeature.INDENT_OUTPUT);
        JsonNode raiz = json.readTree(servido);

        List<String> operacoes = new ArrayList<>();
        raiz.get("paths").properties().forEach(caminho -> caminho.getValue().properties().forEach(op -> {
            String nome = op.getKey().toUpperCase() + " " + caminho.getKey();
            operacoes.add(nome);
            JsonNode operacao = op.getValue();
            assertThat(operacao.path("summary").asText()).as(nome + " sem resumo").isNotBlank();
            assertThat(operacao.path("tags")).as(nome + " sem tag").isNotEmpty();
            List<String> erros = new ArrayList<>();
            operacao.get("responses").fieldNames().forEachRemaining(c -> {
                if (c.startsWith("4")) {
                    erros.add(c);
                }
            });
            assertThat(erros).as(nome + " sem erro documentado").isNotEmpty();
            erros.forEach(c -> assertThat(operacao.at("/responses/" + c + "/content/application~1problem+json/schema/$ref")
                    .asText()).as(nome + " " + c).isEqualTo("#/components/schemas/Problema"));
        }));
        assertThat(operacoes).isNotEmpty();
        assertThat(raiz.at("/components/schemas/Problema/properties/regra")).isNotEmpty();

        String formatado = json.writeValueAsString(raiz) + "\n";
        if ("1".equals(System.getenv("SOMBRA_ATUALIZAR_OPENAPI"))) {
            Files.writeString(CONTRATO, formatado, StandardCharsets.UTF_8);
        }
        assertThat(CONTRATO).as("Falta o contrato: rode make openapi-clima.").exists();
        assertThat(Files.readString(CONTRATO, StandardCharsets.UTF_8))
                .as("A API mudou e o contrato não: rode make openapi-clima e revise o diff.")
                .isEqualTo(formatado);
    }
}
