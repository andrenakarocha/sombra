package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.PrevisorDeCalor;
import br.com.fiap.sombra.clima.dominio.RegraPrevisorDeCalor;
import org.springframework.boot.autoconfigure.condition.ConditionalOnMissingBean;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/** Sem o modelo de ML (F9.1), o previsor é a regra de persistência do pico. */
@Configuration(proxyBeanMethods = false)
public class ConfiguracaoDoPrevisor {

    @Bean
    @ConditionalOnMissingBean(PrevisorDeCalor.class)
    PrevisorDeCalor previsorPorRegra() {
        return new RegraPrevisorDeCalor();
    }
}
