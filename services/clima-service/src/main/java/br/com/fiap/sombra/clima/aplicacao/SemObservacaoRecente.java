package br.com.fiap.sombra.clima.aplicacao;

/**
 * A estação da região não tem observação nas últimas horas (a coleta parou ou a fonte caiu). Vira HTTP 503: o Core
 * trata como "Clima indisponível" e segue com o último risco conhecido, em vez de agir sobre um dado velho.
 */
public class SemObservacaoRecente extends RuntimeException {

    private static final long serialVersionUID = 1L;

    public SemObservacaoRecente(String regiao, String estacao) {
        super("A estação " + estacao + " da região " + regiao + " não tem observação nas últimas "
                + ConsultaDeRiscoDeCalor.VALIDADE.toHours() + " horas.");
    }
}
