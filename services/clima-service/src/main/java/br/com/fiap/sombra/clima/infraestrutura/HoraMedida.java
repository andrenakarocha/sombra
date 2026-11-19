package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.Medicao;
import java.time.Instant;

/** Uma hora medida por uma fonte externa, já convertida para as unidades do domínio. */
public record HoraMedida(Instant observadaEm, Medicao medicao) {
}
