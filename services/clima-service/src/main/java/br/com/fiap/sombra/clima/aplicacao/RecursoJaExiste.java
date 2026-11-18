package br.com.fiap.sombra.clima.aplicacao;

/** Já existe um recurso com o mesmo identificador. Vira HTTP 409 na API. */
public class RecursoJaExiste extends RuntimeException {

    private static final long serialVersionUID = 1L;

    public RecursoJaExiste(String recurso, Object identificador) {
        super(recurso + " '" + identificador + "' já existe.");
    }
}
