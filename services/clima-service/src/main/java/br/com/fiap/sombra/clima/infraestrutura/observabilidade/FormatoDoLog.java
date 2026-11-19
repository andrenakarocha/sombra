package br.com.fiap.sombra.clima.infraestrutura.observabilidade;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.classic.spi.IThrowableProxy;
import ch.qos.logback.classic.spi.ThrowableProxyUtil;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.format.DateTimeFormatter;
import java.util.LinkedHashMap;
import java.util.Map;
import org.slf4j.event.KeyValuePair;
import org.springframework.boot.logging.structured.StructuredLogFormatter;
import org.springframework.core.env.Environment;

/**
 * O log do Clima no formato de {@code docs/observabilidade.md} §1, o mesmo do Core: uma linha JSON por evento, com
 * timestamp (UTC, milissegundos), level, service, version, environment, correlationId, event e message, mais os campos
 * do evento em camelCase (passados como key-value do SLF4J). Stack trace só aqui, no campo {@code exception}.
 */
public class FormatoDoLog implements StructuredLogFormatter<ILoggingEvent> {

    private static final DateTimeFormatter TIMESTAMP =
            DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'").withZone(ZoneOffset.UTC);
    private static final ObjectMapper JSON = new ObjectMapper();

    private final String versao;
    private final String ambiente;

    public FormatoDoLog(Environment ambiente) {
        this.versao = ambiente.getProperty("info.commit", "dev");
        this.ambiente = ambiente.getProperty("sombra.ambiente", "Development");
    }

    @Override
    public String format(ILoggingEvent evento) {
        Map<String, Object> linha = new LinkedHashMap<>();
        linha.put("timestamp", TIMESTAMP.format(Instant.ofEpochMilli(evento.getTimeStamp())));
        linha.put("level", nivel(evento.getLevel()));
        linha.put("service", "clima-service");
        linha.put("version", versao);
        linha.put("environment", ambiente);
        linha.put("correlationId", evento.getMDCPropertyMap().get("correlationId"));
        linha.put("event", "log");
        linha.put("message", evento.getFormattedMessage());
        if (evento.getKeyValuePairs() != null) {
            for (KeyValuePair par : evento.getKeyValuePairs()) {
                linha.put(par.key, par.value instanceof Number || par.value instanceof Boolean ? par.value : String.valueOf(par.value));
            }
        }
        IThrowableProxy excecao = evento.getThrowableProxy();
        if (excecao != null) {
            linha.put("exception", ThrowableProxyUtil.asString(excecao));
        }
        try {
            return JSON.writeValueAsString(linha) + "\n";
        } catch (JsonProcessingException e) {
            return "{\"level\":\"Error\",\"service\":\"clima-service\",\"event\":\"log\",\"message\":\"linha de log ilegível\"}\n";
        }
    }

    static String nivel(Level level) {
        return switch (level.toInt()) {
            case Level.ERROR_INT -> "Error";
            case Level.WARN_INT -> "Warning";
            case Level.INFO_INT -> "Information";
            default -> "Debug";
        };
    }
}
