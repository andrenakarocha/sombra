package br.com.fiap.sombra.clima.api;

import io.swagger.v3.oas.annotations.media.Schema;
import java.util.List;
import java.util.Map;

/** Só para a documentação: o formato ProblemDetails (RFC 9457) que o {@link Erros} devolve. */
@Schema(name = "Problema", description = "Erro no formato ProblemDetails (RFC 9457), sempre em pt-BR e sem dado interno")
record Problema(
        @Schema(example = "https://github.com/andrenakarocha/sombra/blob/main/docs/contratos/clima-service.md#erros") String type,
        @Schema(example = "Regra de negócio violada") String title,
        @Schema(example = "422") int status,
        @Schema(example = "O valor -9999 é o código de dado faltante do INMET: não é uma observação.") String detail,
        @Schema(example = "/api/v1/estacoes/A701/observacoes") String instance,
        @Schema(description = "Código estável da regra violada (só no 422)", example = "OBSERVACAO_FALTANTE") String regra,
        @Schema(description = "Para cruzar com o log (§3 da observabilidade)", example = "4f1c2b7e9a0d4e6f") String correlationId,
        @Schema(description = "Erros por campo ou parâmetro (só no 400)") Map<String, List<String>> errors) {
}
