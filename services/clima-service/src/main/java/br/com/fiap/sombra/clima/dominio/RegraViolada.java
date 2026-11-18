package br.com.fiap.sombra.clima.dominio;

import java.util.Objects;

/**
 * Uma regra do domínio foi quebrada. Vira HTTP 422 na API, com o código estável da regra no campo {@code regra}.
 */
public class RegraViolada extends RuntimeException {

    private static final long serialVersionUID = 1L;

    private final String regra;

    public RegraViolada(String regra, String mensagem) {
        super(mensagem);
        this.regra = Objects.requireNonNull(regra, "regra");
    }

    /** O código estável da regra (ex.: {@code OBSERVACAO_FALTANTE}), que o consumidor pode tratar sem ler a frase. */
    public String getRegra() {
        return regra;
    }
}
