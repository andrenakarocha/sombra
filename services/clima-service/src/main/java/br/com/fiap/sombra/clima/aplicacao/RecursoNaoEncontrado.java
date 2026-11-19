package br.com.fiap.sombra.clima.aplicacao;

/** O recurso pedido não existe. Vira HTTP 404 na API. */
public class RecursoNaoEncontrado extends RuntimeException {

    private static final long serialVersionUID = 1L;

    /** @param recurso o nome do recurso em minúsculas (ex.: "região climática") */
    public RecursoNaoEncontrado(String recurso, Object identificador) {
        super("Não existe " + recurso + " '" + identificador + "'.");
    }
}
