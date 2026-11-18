package br.com.fiap.sombra.clima.aplicacao;

/** O recurso pedido não existe. Vira HTTP 404 na API. */
public class RecursoNaoEncontrado extends RuntimeException {

    private static final long serialVersionUID = 1L;

    public RecursoNaoEncontrado(String recurso, Object identificador) {
        super(recurso + " '" + identificador + "' não encontrado(a).");
    }
}
