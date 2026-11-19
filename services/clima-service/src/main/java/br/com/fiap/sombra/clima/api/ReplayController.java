package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.aplicacao.EstadoDoReplay;
import br.com.fiap.sombra.clima.aplicacao.ReplayInmet;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.media.Schema;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import java.time.Instant;
import java.time.OffsetDateTime;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = DocumentacaoDaApi.REPLAY)
@RestController
@RequestMapping("/api/v1/replay")
public class ReplayController {

    private final ReplayInmet replay;

    public ReplayController(ReplayInmet replay) {
        this.replay = replay;
    }

    @Schema(name = "PedidoDeReplay", description = "Qual onda de calor real tocar, e com que velocidade")
    public record PedidoDeReplay(
            @NotBlank @Schema(example = "SP-MIRANTE") String regiao,
            @NotNull @Schema(example = "2023-11-12T00:00:00-03:00", description = "Hora real do INMET em que o replay começa")
            OffsetDateTime inicio,
            @Min(1) @Max(3600) @Schema(example = "60", description = "Horas de dado por hora real (60 = uma hora de dado por minuto)")
            Integer velocidade) {
    }

    @Schema(name = "Replay", description = "O estado do replay")
    public record ReplaySaida(
            @Schema(example = "true") boolean ativo,
            @Schema(example = "SP-MIRANTE") String regiao,
            @Schema(example = "2023-11-12T03:00:00Z") Instant inicio,
            @Schema(example = "2023-11-30T23:00:00Z", description = "Última hora com dado: passou dela, o replay encerra sozinho") Instant fim,
            @Schema(example = "60") Integer velocidade,
            @Schema(example = "2023-11-12T05:00:00Z", description = "O \"agora\" do replay") Instant instanteAtual) {

        static ReplaySaida de(EstadoDoReplay e) {
            return new ReplaySaida(true, e.regiao(), e.inicio(), e.fim(), e.velocidade(), e.instanteAtual());
        }

        static final ReplaySaida DESLIGADO = new ReplaySaida(false, null, null, null, null, null);
    }

    @Operation(operationId = "iniciarReplay", summary = "Liga o replay de uma onda de calor real do INMET (modo de demonstração)",
            description = "Toca as horas reais gravadas no seed (A701, nov/2023) num relógio acelerado. Enquanto durar, "
                    + "o risco da região sai com fonteDados = replay-inmet. Um replay ativo é trocado pelo novo.")
    @ApiResponse(responseCode = "200", description = "Replay ligado")
    @ErrosDocumentados({400, 404, 422})
    @PostMapping
    public ReplaySaida iniciar(@Valid @RequestBody PedidoDeReplay pedido) {
        int velocidade = pedido.velocidade() == null ? 60 : pedido.velocidade();
        return ReplaySaida.de(replay.iniciar(pedido.regiao(), pedido.inicio().toInstant(), velocidade));
    }

    @Operation(operationId = "estadoDoReplay", summary = "Mostra o estado do replay", description = "Com o replay desligado, responde ativo = false.")
    @ApiResponse(responseCode = "200", description = "O estado do replay")
    @ErrosDocumentados({400})
    @GetMapping
    public ReplaySaida estado() {
        return replay.estado().map(ReplaySaida::de).orElse(ReplaySaida.DESLIGADO);
    }

    @Operation(operationId = "encerrarReplay", summary = "Desliga o replay", description = "Idempotente: desligar sem replay ativo também responde 204.")
    @ApiResponse(responseCode = "204", description = "Replay desligado")
    @ErrosDocumentados({400})
    @DeleteMapping
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void encerrar() {
        replay.encerrar();
    }
}
