package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.api.ObservacaoDtos.NovaObservacaoEntrada;
import br.com.fiap.sombra.clima.api.ObservacaoDtos.ObservacaoSaida;
import br.com.fiap.sombra.clima.aplicacao.NovaObservacao;
import br.com.fiap.sombra.clima.aplicacao.RegistroDeObservacoes;
import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.Periodo;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import java.time.Instant;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = DocumentacaoDaApi.OBSERVACOES)
@RestController
@RequestMapping("/api/v1/estacoes/{codigo}/observacoes")
public class ObservacoesController {

    private final RegistroDeObservacoes registro;

    public ObservacoesController(RegistroDeObservacoes registro) {
        this.registro = registro;
    }

    @Operation(summary = "Registra a observação de uma hora numa estação",
            description = "Calcula o índice de calor e o nível. Recusa o -9999 do INMET (OBSERVACAO_FALTANTE), umidade fora de 0–100 % e a mesma hora duas vezes.")
    @ApiResponse(responseCode = "201", description = "Observação registrada")
    @ErrosDocumentados({400, 404, 409, 422})
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public ObservacaoSaida registrar(@Parameter(example = "A701") @PathVariable String codigo,
            @Valid @RequestBody NovaObservacaoEntrada dados) {
        return ObservacaoSaida.de(registro.registrar(codigo, new NovaObservacao(dados.observadaEm(), dados.temperaturaC(),
                dados.umidadePct(), dados.pressaoHpa(), dados.radiacaoKjM2(), dados.ventoMs(), dados.precipitacaoMm(),
                FonteDaObservacao.INMET)));
    }

    @Operation(summary = "Lista as observações de uma estação num período, da mais recente para a mais antiga",
            description = "Período [de, ate) de no máximo 31 dias (regra PERIODO_INVALIDO), paginado.")
    @ApiResponse(responseCode = "200", description = "A página de observações")
    @ErrosDocumentados({400, 404, 422})
    @GetMapping
    public Pagina<ObservacaoSaida> noPeriodo(@Parameter(example = "A701") @PathVariable String codigo,
            @Parameter(example = "2023-11-15T00:00:00Z") @RequestParam Instant de,
            @Parameter(example = "2023-11-16T00:00:00Z") @RequestParam Instant ate,
            @Parameter(example = "1") @RequestParam(defaultValue = "1") @Min(1) int pagina,
            @Parameter(example = "24") @RequestParam(defaultValue = "20") @Min(1) @Max(100) int tamanho) {
        return Pagina.de(registro.noPeriodo(codigo, new Periodo(de, ate), pagina, tamanho), ObservacaoSaida::de);
    }
}
