package br.com.fiap.sombra.clima.infraestrutura.observabilidade;

import br.com.fiap.sombra.clima.infraestrutura.ConfiguracaoOpenMeteo;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.context.event.ApplicationReadyEvent;
import org.springframework.context.event.ContextClosedEvent;
import org.springframework.context.event.EventListener;
import org.springframework.core.env.Environment;
import org.springframework.stereotype.Component;

/** {@code servico.iniciado} (com a versão e a configuração, sem segredo) e {@code servico.encerrado}. */
@Component
public class CicloDeVida {

    private static final Logger LOG = LoggerFactory.getLogger(CicloDeVida.class);

    private final Environment ambiente;
    private final ConfiguracaoOpenMeteo openMeteo;

    public CicloDeVida(Environment ambiente, ConfiguracaoOpenMeteo openMeteo) {
        this.ambiente = ambiente;
        this.openMeteo = openMeteo;
    }

    @EventListener(ApplicationReadyEvent.class)
    public void iniciado() {
        LOG.atInfo().addKeyValue("event", "servico.iniciado")
                .addKeyValue("porta", ambiente.getProperty("local.server.port", ambiente.getProperty("server.port", "")))
                .addKeyValue("banco", semCredencial(ambiente.getProperty("spring.datasource.url", "")))
                .addKeyValue("coletaAoVivo", openMeteo.habilitado())
                .addKeyValue("intervaloDaColeta", String.valueOf(openMeteo.intervalo()))
                .log("Clima Service iniciado");
    }

    @EventListener(ContextClosedEvent.class)
    public void encerrado() {
        LOG.atInfo().addKeyValue("event", "servico.encerrado").log("Clima Service encerrado");
    }

    /** A URL JDBC sem usuário, senha nem parâmetros: só host, porta e banco. */
    static String semCredencial(String url) {
        String semParametros = url.contains("?") ? url.substring(0, url.indexOf('?')) : url;
        return semParametros.replaceAll("//[^/@]*@", "//");
    }
}
