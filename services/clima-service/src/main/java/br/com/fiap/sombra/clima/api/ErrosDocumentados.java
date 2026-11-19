package br.com.fiap.sombra.clima.api;

import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * Os códigos de erro que um endpoint pode devolver. A {@link DocumentacaoDaApi} transforma cada um numa resposta do
 * OpenAPI com o corpo {@code Problema} e um exemplo, para o erro fazer parte do contrato e não só o caminho feliz.
 */
@Documented
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.METHOD)
public @interface ErrosDocumentados {

    /** Os status HTTP de erro (400, 404, 409, 422, 503). */
    int[] value();
}
