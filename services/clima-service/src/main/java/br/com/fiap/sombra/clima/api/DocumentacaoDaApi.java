package br.com.fiap.sombra.clima.api;

import io.swagger.v3.core.converter.ModelConverters;
import io.swagger.v3.oas.models.Components;
import io.swagger.v3.oas.models.OpenAPI;
import io.swagger.v3.oas.models.examples.Example;
import io.swagger.v3.oas.models.info.Contact;
import io.swagger.v3.oas.models.info.Info;
import io.swagger.v3.oas.models.info.License;
import io.swagger.v3.oas.models.media.Content;
import io.swagger.v3.oas.models.media.MediaType;
import io.swagger.v3.oas.models.media.Schema;
import io.swagger.v3.oas.models.responses.ApiResponse;
import io.swagger.v3.oas.models.responses.ApiResponses;
import io.swagger.v3.oas.models.servers.Server;
import io.swagger.v3.oas.models.tags.Tag;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import org.springdoc.core.customizers.OpenApiCustomizer;
import org.springdoc.core.customizers.OperationCustomizer;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/** O contrato OpenAPI gerado do código: descrição, tags, o corpo {@code Problema} e os erros de cada endpoint. */
@Configuration(proxyBeanMethods = false)
public class DocumentacaoDaApi {

    static final String REGIOES = "Regiões climáticas";
    static final String OBSERVACOES = "Observações";
    static final String RISCO = "Risco de calor";
    static final String PONTOS = "Pontos de resfriamento";
    static final String REPLAY = "Replay (demonstração)";

    private static final String PROBLEMA = "#/components/schemas/Problema";

    private static final Map<Integer, String> DESCRICOES = Map.of(
            400, "Requisição inválida: campo ou parâmetro fora do formato (os detalhes vêm em `errors`)",
            404, "Recurso não encontrado",
            409, "Já existe um recurso com o mesmo identificador",
            422, "Regra de negócio violada (o código estável vem em `regra`)",
            503, "Sem observação recente da estação: o consumidor segue com o último risco conhecido");

    private static final Map<Integer, Map<String, Object>> EXEMPLOS = Map.of(
            400, Map.of("title", "Requisição inválida", "status", 400, "detail", "Um ou mais campos são inválidos.",
                    "errors", Map.of("nome", List.of("não deve estar em branco"))),
            404, Map.of("title", "Recurso não encontrado", "status", 404,
                    "detail", "Não existe região climática 'SP-XX'."),
            409, Map.of("title", "Conflito", "status", 409, "detail", "Já existe região climática 'SP-MIRANTE'."),
            422, Map.of("title", "Regra de negócio violada", "status", 422,
                    "detail", "O valor -9999 é o código de dado faltante do INMET: não é uma observação.",
                    "regra", "OBSERVACAO_FALTANTE"),
            503, Map.of("title", "Dado climático indisponível", "status", 503,
                    "detail", "A estação A701 da região SP-MIRANTE não tem observação nas últimas 3 horas."));

    @Bean
    OpenAPI contratoDoClima() {
        return new OpenAPI()
                .info(new Info()
                        .title("Sombra · Clima Service")
                        .version("v1")
                        .description("""
                                O dono do conhecimento climático do Sombra: regiões, estações do INMET, observações \
                                horárias, risco de calor agora e em 24 h e pontos de resfriamento perto de casa. O Core é \
                                o único cliente. Erros em ProblemDetails (RFC 9457), tempo em ISO-8601 UTC e o header \
                                `X-Correlation-Id` aceito e devolvido. Não guarda dado pessoal.""")
                        .contact(new Contact().name("Sombra · GS 2026.2 FIAP").url("https://github.com/andrenakarocha/sombra"))
                        .license(new License().name("MIT").url("https://github.com/andrenakarocha/sombra/blob/main/LICENSE")))
                .servers(List.of(new Server().url("http://localhost:8081").description("ambiente local (make up)")))
                .tags(List.of(
                        new Tag().name(REGIOES).description("A área atendida por uma estação de referência (CRUD)"),
                        new Tag().name(OBSERVACOES).description("Medições horárias das estações do INMET"),
                        new Tag().name(RISCO).description("O nível de risco agora e a previsão de 24 h"),
                        new Tag().name(PONTOS).description("Lugares públicos e frescos (CRUD e busca por proximidade)"),
                        new Tag().name(REPLAY).description("Toca a onda de calor real de nov/2023 acelerada, para a demonstração")));
    }

    /** Registra o esquema {@code Problema} e ordena os componentes (o contrato versionado fica estável). */
    @Bean
    OpenApiCustomizer esquemaDoProblema() {
        return openApi -> {
            Components componentes = openApi.getComponents() == null ? new Components() : openApi.getComponents();
            ModelConverters.getInstance().read(Problema.class).forEach(componentes::addSchemas);
            componentes.setSchemas(new TreeMap<>(componentes.getSchemas()));
            openApi.setComponents(componentes);
        };
    }

    /** Cada {@link ErrosDocumentados} vira uma resposta com o corpo {@code Problema} e um exemplo. */
    @Bean
    OperationCustomizer errosDocumentados() {
        return (operacao, metodo) -> {
            ErrosDocumentados erros = metodo.getMethodAnnotation(ErrosDocumentados.class);
            if (erros == null) {
                return operacao;
            }
            ApiResponses respostas = operacao.getResponses() == null ? new ApiResponses() : operacao.getResponses();
            for (int status : erros.value()) {
                Map<String, Object> exemplo = new LinkedHashMap<>();
                exemplo.put("type", Erros.TIPO.toString());
                exemplo.putAll(EXEMPLOS.get(status));
                exemplo.put("correlationId", "4f1c2b7e9a0d4e6f");
                MediaType corpo = new MediaType()
                        .schema(new Schema<>().$ref(PROBLEMA))
                        .addExamples("exemplo", new Example().value(exemplo));
                respostas.addApiResponse(String.valueOf(status), new ApiResponse()
                        .description(DESCRICOES.get(status))
                        .content(new Content().addMediaType("application/problem+json", corpo)));
            }
            operacao.setResponses(respostas);
            return operacao;
        };
    }
}
