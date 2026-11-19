package br.com.fiap.sombra.clima.dominio;

import java.time.Duration;
import java.time.Instant;

/**
 * Um intervalo de tempo [de, ate) para consultar observações. Regras: começa antes de terminar e tem no máximo 31 dias
 * (uma consulta maior vira exportação, não resposta de API).
 *
 * @param de  início, incluído
 * @param ate fim, excluído
 */
public record Periodo(Instant de, Instant ate) {

    public static final Duration MAXIMO = Duration.ofDays(31);

    public Periodo {
        if (de == null || ate == null || !de.isBefore(ate)) {
            throw new RegraViolada("PERIODO_INVALIDO", "O período precisa começar antes de terminar (de < ate).");
        }
        if (Duration.between(de, ate).compareTo(MAXIMO) > 0) {
            throw new RegraViolada("PERIODO_INVALIDO", "O período pode ter no máximo 31 dias.");
        }
    }
}
