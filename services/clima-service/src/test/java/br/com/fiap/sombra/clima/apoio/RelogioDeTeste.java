package br.com.fiap.sombra.clima.apoio;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneId;
import java.time.ZoneOffset;
import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Primary;

/** Um relógio que o teste adianta à mão: o replay e a coleta usam o {@link Clock} do contexto. */
public class RelogioDeTeste extends Clock {

    private volatile Instant agora;

    public RelogioDeTeste(Instant agora) {
        this.agora = agora;
    }

    public void adiantar(Duration quanto) {
        agora = agora.plus(quanto);
    }

    public void ajustar(Instant instante) {
        agora = instante;
    }

    @Override
    public Instant instant() {
        return agora;
    }

    @Override
    public ZoneId getZone() {
        return ZoneOffset.UTC;
    }

    @Override
    public Clock withZone(ZoneId zone) {
        return this;
    }

    @TestConfiguration(proxyBeanMethods = false)
    public static class Configuracao {

        @Bean
        @Primary
        RelogioDeTeste relogioDeTeste() {
            return new RelogioDeTeste(Instant.parse("2026-11-18T18:00:00Z"));
        }
    }
}
