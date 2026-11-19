package br.com.fiap.sombra.clima.api;

import io.swagger.v3.oas.annotations.media.Schema;
import java.util.List;
import java.util.function.Function;
import org.springframework.data.domain.Page;

/**
 * Uma página de resultados, no mesmo formato do Core: {@code ?pagina=1&tamanho=20} (máximo 100).
 *
 * @param itens   os itens da página
 * @param pagina  o número da página, a partir de 1
 * @param tamanho quantos itens por página
 * @param total   quantos itens existem no total
 */
public record Pagina<T>(
        List<T> itens,
        @Schema(example = "1") int pagina,
        @Schema(example = "20") int tamanho,
        @Schema(example = "57") long total) {

    static <E, T> Pagina<T> de(Page<E> page, Function<E, T> converter) {
        return new Pagina<>(page.getContent().stream().map(converter).toList(), page.getNumber() + 1, page.getSize(),
                page.getTotalElements());
    }
}
