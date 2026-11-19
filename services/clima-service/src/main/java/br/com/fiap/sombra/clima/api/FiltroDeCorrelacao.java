package br.com.fiap.sombra.clima.api;

import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.security.SecureRandom;
import java.util.HexFormat;
import java.util.regex.Pattern;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;
import org.springframework.web.servlet.HandlerMapping;

/**
 * Correlação ponta a ponta (observabilidade §3): aceita o {@code X-Correlation-Id} do Core (até 64 caracteres em
 * {@code [A-Za-z0-9:_-]}), ou gera 16 hex; põe no MDC, devolve no header e fecha com {@code requisicao.concluida}
 * (método, rota-modelo, status e duração; nunca o corpo). As sondas do actuator não entram no log.
 */
@Component
@Order(Ordered.HIGHEST_PRECEDENCE)
public class FiltroDeCorrelacao extends OncePerRequestFilter {

    public static final String HEADER = "X-Correlation-Id";
    static final String CHAVE = "correlationId";

    private static final Pattern FORMATO = Pattern.compile("[A-Za-z0-9:_-]{1,64}");
    private static final SecureRandom ALEATORIO = new SecureRandom();
    private static final Logger LOG = LoggerFactory.getLogger(FiltroDeCorrelacao.class);

    static String novoId() {
        byte[] bytes = new byte[8];
        ALEATORIO.nextBytes(bytes);
        return HexFormat.of().formatHex(bytes);
    }

    @Override
    protected void doFilterInternal(HttpServletRequest requisicao, HttpServletResponse resposta, FilterChain cadeia)
            throws ServletException, IOException {
        String recebido = requisicao.getHeader(HEADER);
        String id = recebido != null && FORMATO.matcher(recebido).matches() ? recebido : novoId();
        MDC.put(CHAVE, id);
        resposta.setHeader(HEADER, id);
        long inicio = System.nanoTime();
        try {
            cadeia.doFilter(requisicao, resposta);
        } finally {
            if (!requisicao.getRequestURI().startsWith("/actuator")) {
                Object rota = requisicao.getAttribute(HandlerMapping.BEST_MATCHING_PATTERN_ATTRIBUTE);
                LOG.atInfo().addKeyValue("event", "requisicao.concluida")
                        .addKeyValue("metodo", requisicao.getMethod())
                        .addKeyValue("rota", rota == null ? "(sem rota)" : rota.toString())
                        .addKeyValue("status", resposta.getStatus())
                        .addKeyValue("duracaoMs", (System.nanoTime() - inicio) / 1_000_000)
                        .log("{} {} {}", requisicao.getMethod(), rota == null ? "(sem rota)" : rota, resposta.getStatus());
            }
            MDC.remove(CHAVE);
        }
    }
}
