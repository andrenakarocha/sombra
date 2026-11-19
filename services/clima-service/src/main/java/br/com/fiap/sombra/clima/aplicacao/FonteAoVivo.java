package br.com.fiap.sombra.clima.aplicacao;

import java.time.Clock;
import java.time.Instant;
import org.springframework.stereotype.Component;

/** O modo normal: o relógio de verdade, com as horas que a {@link ColetaAoVivo} grava da Open-Meteo. */
@Component
public class FonteAoVivo implements FonteMeteorologica {

    private final Clock relogio;

    public FonteAoVivo(Clock relogio) {
        this.relogio = relogio;
    }

    @Override
    public String nome() {
        return "open-meteo";
    }

    @Override
    public Instant agora() {
        return relogio.instant();
    }
}
