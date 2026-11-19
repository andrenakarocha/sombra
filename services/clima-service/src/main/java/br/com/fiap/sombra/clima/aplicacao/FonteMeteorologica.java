package br.com.fiap.sombra.clima.aplicacao;

import java.time.Instant;

/**
 * De onde vem o "agora" de uma região: a Open-Meteo ao vivo ou o replay de uma onda de calor real do INMET. As duas
 * respondem pelas observações gravadas; muda o relógio e o nome que sai no risco ({@code fonteDados}).
 */
public interface FonteMeteorologica {

    /** {@code open-meteo} ou {@code replay-inmet}, como sai no contrato. */
    String nome();

    /** O instante de referência desta fonte: o relógio real ou o relógio acelerado do replay. */
    Instant agora();
}
