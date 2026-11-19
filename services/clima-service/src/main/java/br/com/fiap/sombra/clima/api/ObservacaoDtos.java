package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.NivelDeRisco;
import br.com.fiap.sombra.clima.dominio.Observacao;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;
import java.time.Instant;

/** Entrada e saída das observações horárias. O índice de calor e o nível nunca vêm de fora: o domínio calcula. */
public final class ObservacaoDtos {

    private ObservacaoDtos() {
    }

    @Schema(name = "NovaObservacao", description = "O que a estação mediu numa hora. -9999 (dado faltante do INMET) é recusado com 422.")
    public record NovaObservacaoEntrada(
            @NotNull @Schema(example = "2023-11-15T18:00:00Z", description = "Hora da medição, em UTC") Instant observadaEm,
            @NotNull @Schema(example = "35.7", description = "Temperatura do ar, °C") Double temperaturaC,
            @NotNull @Schema(example = "29", description = "Umidade relativa, %") Double umidadePct,
            @Schema(example = "919.6", description = "Pressão ao nível da estação, hPa") Double pressaoHpa,
            @Schema(example = "2741.9", description = "Radiação global na hora, kJ/m²") Double radiacaoKjM2,
            @Schema(example = "1.8", description = "Velocidade do vento, m/s") Double ventoMs,
            @Schema(example = "0", description = "Precipitação na hora, mm") Double precipitacaoMm) {
    }

    @Schema(name = "Observacao", description = "Uma observação horária, com o índice de calor e o nível calculados pelo Clima")
    public record ObservacaoSaida(
            @Schema(example = "A701") String estacao,
            @Schema(example = "2023-11-15T18:00:00Z") Instant observadaEm,
            @Schema(example = "35.7") double temperaturaC,
            @Schema(example = "29") double umidadePct,
            @Schema(example = "919.6") Double pressaoHpa,
            @Schema(example = "2741.9") Double radiacaoKjM2,
            @Schema(example = "1.8") Double ventoMs,
            @Schema(example = "0") Double precipitacaoMm,
            @Schema(example = "35.47") double indiceDeCalorC,
            @Schema(example = "CAUTELA_EXTREMA") NivelDeRisco nivel,
            @Schema(example = "INMET") FonteDaObservacao fonte) {

        static ObservacaoSaida de(Observacao o) {
            var m = o.getMedicao();
            return new ObservacaoSaida(o.getEstacao().getCodigo(), o.getObservadaEm(), m.temperaturaC(), m.umidadePct(),
                    m.pressaoHpa(), m.radiacaoKjM2(), m.ventoMs(), m.precipitacaoMm(), o.getIndiceDeCalorC(), o.getNivel(),
                    o.getFonte());
        }
    }
}
