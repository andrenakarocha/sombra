package br.com.fiap.sombra.clima.aplicacao;

import java.util.Locale;

/** Os códigos chegam pela URL como o cliente escreveu; o domínio guarda em maiúsculas e sem espaços. */
final class Codigos {

    private Codigos() {
    }

    static String normalizar(String codigo) {
        return codigo == null ? "" : codigo.trim().toUpperCase(Locale.ROOT);
    }
}
