package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.aplicacao.ConsultaDeRiscoDeCalor;
import br.com.fiap.sombra.clima.aplicacao.RiscoDeCalor;
import br.com.fiap.sombra.clima.dominio.NivelDeRisco;
import br.com.fiap.sombra.clima.dominio.Observacao;
import br.com.fiap.sombra.clima.dominio.PrevisaoDeCalor;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import java.time.Instant;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = DocumentacaoDaApi.RISCO)
@RestController
@RequestMapping("/api/v1/regioes/{codigo}/risco-calor")
public class RiscoDeCalorController {

    private final ConsultaDeRiscoDeCalor consulta;

    public RiscoDeCalorController(ConsultaDeRiscoDeCalor consulta) {
        this.consulta = consulta;
    }

    @Schema(name = "RiscoAgora", description = "A observação mais recente da estação de referência")
    public record AgoraSaida(
            @Schema(example = "2023-11-15T18:00:00Z") Instant observadaEm,
            @Schema(example = "35.7") double temperaturaC,
            @Schema(example = "29") double umidadePct,
            @Schema(example = "35.47") double indiceDeCalorC,
            @Schema(example = "CAUTELA_EXTREMA") NivelDeRisco nivel) {

        static AgoraSaida de(Observacao o) {
            return new AgoraSaida(o.getObservadaEm(), o.getMedicao().temperaturaC(), o.getMedicao().umidadePct(),
                    o.getIndiceDeCalorC(), o.getNivel());
        }
    }

    @Schema(name = "Previsao24h", description = "A previsão das próximas 24 h")
    public record PrevisaoSaida(
            @Schema(example = "0.07", description = "Probabilidade de chegar a PERIGO, de 0 a 1") double probabilidadePerigo,
            @Schema(example = "CAUTELA_EXTREMA") NivelDeRisco nivelPrevisto,
            @Schema(example = "35.47", description = "Pico previsto do índice de calor, °C") double indiceDeCalorPrevistoC,
            @Schema(example = "24") int horizonteHoras,
            @Schema(example = "regra-persistencia-v1") String modelo,
            @Schema(example = "regra", allowableValues = {"onnx", "regra"}) String fonte) {

        static PrevisaoSaida de(PrevisaoDeCalor p) {
            return new PrevisaoSaida(p.probabilidadePerigo(), p.nivelPrevisto(), p.indiceDeCalorPrevistoC(),
                    p.horizonteHoras(), p.modelo(), p.fonte());
        }
    }

    @Schema(name = "RiscoDeCalor", description = "O risco de calor de uma região agora e nas próximas 24 h")
    public record RiscoSaida(
            @Schema(example = "SP-MIRANTE") String regiao,
            AgoraSaida agora,
            PrevisaoSaida previsao24h,
            @Schema(example = "replay-inmet", allowableValues = {"open-meteo", "replay-inmet"}) String fonteDados) {

        static RiscoSaida de(RiscoDeCalor r) {
            return new RiscoSaida(r.regiao(), AgoraSaida.de(r.agora()), PrevisaoSaida.de(r.previsao24h()), r.fonteDados());
        }
    }

    @Operation(operationId = "consultarRiscoDeCalor", summary = "Consulta o risco de calor de uma região agora e nas próximas 24 h",
            description = "O agora é a observação mais recente da estação de referência (no relógio do replay, se ele "
                    + "estiver ligado). A previsão usa o modelo de ML (fonte = onnx) ou, sem ele, a regra de persistência "
                    + "do pico (fonte = regra). Sem observação nas últimas 3 h, responde 503.")
    @ApiResponse(responseCode = "200", description = "O risco da região")
    @ErrosDocumentados({404, 503})
    @GetMapping
    public RiscoSaida consultar(@Parameter(example = "SP-MIRANTE") @PathVariable String codigo) {
        return RiscoSaida.de(consulta.consultar(codigo));
    }
}
