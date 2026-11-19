package br.com.fiap.sombra.clima.infraestrutura;

import java.time.Clock;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;

/** O relógio (trocado nos testes e conferido pelo replay) e a configuração da Open-Meteo. */
@Configuration(proxyBeanMethods = false)
@EnableConfigurationProperties(ConfiguracaoOpenMeteo.class)
public class ConfiguracaoDaInfraestrutura {

    @Bean
    Clock relogio() {
        return Clock.systemUTC();
    }

    /** A coleta agendada só liga quando {@code clima.open-meteo.habilitado=true} (compose e VM; nunca nos testes). */
    @Configuration(proxyBeanMethods = false)
    @EnableScheduling
    @ConditionalOnProperty(name = "clima.open-meteo.habilitado", havingValue = "true")
    static class Agendamento {
    }
}
