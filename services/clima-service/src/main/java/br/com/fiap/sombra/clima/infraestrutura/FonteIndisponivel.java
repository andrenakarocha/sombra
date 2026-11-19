package br.com.fiap.sombra.clima.infraestrutura;

/** A fonte meteorológica externa não respondeu depois de todas as tentativas. */
public class FonteIndisponivel extends RuntimeException {

    private static final long serialVersionUID = 1L;

    public FonteIndisponivel(String mensagem, Throwable causa) {
        super(mensagem, causa);
    }
}
