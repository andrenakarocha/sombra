package br.com.fiap.sombra.clima.infraestrutura.observabilidade;

import br.com.fiap.sombra.clima.dominio.PrevisorDeCalor;
import br.com.fiap.sombra.clima.dominio.RegraPrevisorDeCalor;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.HealthIndicator;
import org.springframework.stereotype.Component;

/**
 * O previsor aparece como detalhe da prontidão: sempre UP, porque a regra mantém o serviço útil sem o modelo de ML; o
 * detalhe diz qual dos dois está respondendo.
 */
@Component("previsor")
public class SaudeDoPrevisor implements HealthIndicator {

    private final PrevisorDeCalor previsor;

    public SaudeDoPrevisor(PrevisorDeCalor previsor) {
        this.previsor = previsor;
    }

    @Override
    public Health health() {
        boolean regra = previsor instanceof RegraPrevisorDeCalor;
        return Health.up()
                .withDetail("fonte", regra ? "regra" : "onnx")
                .withDetail("modelo", regra ? RegraPrevisorDeCalor.MODELO : previsor.getClass().getSimpleName())
                .build();
    }
}
