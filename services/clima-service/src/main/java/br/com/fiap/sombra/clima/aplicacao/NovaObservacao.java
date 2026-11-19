package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import java.time.Instant;

/** O que chega para virar observação: valores brutos, que o domínio confere ao montar a {@code Medicao}. */
public record NovaObservacao(
        Instant observadaEm,
        double temperaturaC,
        double umidadePct,
        Double pressaoHpa,
        Double radiacaoKjM2,
        Double ventoMs,
        Double precipitacaoMm,
        FonteDaObservacao fonte) {
}
