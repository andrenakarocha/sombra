package br.com.fiap.sombra.clima.api;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.delete;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.get;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.post;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.content;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.jsonPath;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.status;

import br.com.fiap.sombra.clima.aplicacao.RecursoJaExiste;
import br.com.fiap.sombra.clima.aplicacao.RecursoNaoEncontrado;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.web.servlet.WebMvcTest;
import org.springframework.context.annotation.Import;
import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RestController;

/** O contrato dos erros: cada falha vira um ProblemDetails em pt-BR, com o status certo e sem stack trace. */
@WebMvcTest(controllers = ErrosTest.Falhas.class)
@Import(ErrosTest.Falhas.class)
class ErrosTest {

    @Autowired
    MockMvc mvc;

    @Test
    void regraDoDominioVira422ComOCodigoDaRegra() throws Exception {
        mvc.perform(get("/teste/regra"))
                .andExpect(status().isUnprocessableEntity())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_PROBLEM_JSON))
                .andExpect(jsonPath("$.type").value(Erros.TIPO.toString()))
                .andExpect(jsonPath("$.title").value("Regra de negócio violada"))
                .andExpect(jsonPath("$.status").value(422))
                .andExpect(jsonPath("$.detail").value("-9999 é dado faltante do INMET, não é observação."))
                .andExpect(jsonPath("$.regra").value("OBSERVACAO_FALTANTE"));
    }

    @Test
    void recursoInexistenteVira404() throws Exception {
        mvc.perform(get("/teste/nao-existe/SP-XX"))
                .andExpect(status().isNotFound())
                .andExpect(jsonPath("$.title").value("Recurso não encontrado"))
                .andExpect(jsonPath("$.detail").value("Região climática 'SP-XX' não encontrado(a)."));
    }

    @Test
    void duplicadoVira409() throws Exception {
        mvc.perform(get("/teste/duplicado"))
                .andExpect(status().isConflict())
                .andExpect(jsonPath("$.title").value("Conflito"));
    }

    @Test
    void campoInvalidoVira400ComOsErrosPorCampo() throws Exception {
        mvc.perform(post("/teste/validacao").contentType(MediaType.APPLICATION_JSON).content("{\"nome\": \"\"}"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.title").value("Requisição inválida"))
                .andExpect(jsonPath("$.errors.nome").isArray());
    }

    @Test
    void jsonQuebradoVira400EmPortugues() throws Exception {
        mvc.perform(post("/teste/validacao").contentType(MediaType.APPLICATION_JSON).content("{"))
                .andExpect(status().isBadRequest())
                .andExpect(jsonPath("$.detail").value("O corpo da requisição não é um JSON válido para este recurso."));
    }

    @Test
    void metodoNaoAceitoVira405() throws Exception {
        mvc.perform(delete("/teste/regra"))
                .andExpect(status().isMethodNotAllowed())
                .andExpect(jsonPath("$.detail").value("O método DELETE não é aceito nesta rota."));
    }

    @Test
    void falhaInesperadaNaoVazaODetalheInterno() throws Exception {
        mvc.perform(get("/teste/inesperado"))
                .andExpect(status().isInternalServerError())
                .andExpect(jsonPath("$.detail").value("Erro interno. Informe o código do erro ao suporte."))
                .andExpect(jsonPath("$.trace").doesNotExist())
                .andExpect(content().string(org.hamcrest.Matchers.not(org.hamcrest.Matchers.containsString("segredo"))));
    }

    @RestController
    static class Falhas {

        record Entrada(@NotBlank String nome) {
        }

        @GetMapping("/teste/regra")
        String regra() {
            throw new RegraViolada("OBSERVACAO_FALTANTE", "-9999 é dado faltante do INMET, não é observação.");
        }

        @GetMapping("/teste/nao-existe/{codigo}")
        String naoExiste(@PathVariable String codigo) {
            throw new RecursoNaoEncontrado("Região climática", codigo);
        }

        @GetMapping("/teste/duplicado")
        String duplicado() {
            throw new RecursoJaExiste("Região climática", "SP-MIRANTE");
        }

        @PostMapping("/teste/validacao")
        String validacao(@Valid @RequestBody Entrada entrada) {
            return entrada.nome();
        }

        @GetMapping("/teste/inesperado")
        String inesperado() {
            throw new IllegalStateException("detalhe interno com segredo");
        }
    }
}
