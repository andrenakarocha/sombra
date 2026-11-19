package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.api.PontoDtos.PontoEntrada;
import br.com.fiap.sombra.clima.api.PontoDtos.PontoProximoSaida;
import br.com.fiap.sombra.clima.api.PontoDtos.PontoSaida;
import br.com.fiap.sombra.clima.aplicacao.CadastroDePontos;
import br.com.fiap.sombra.clima.dominio.Coordenada;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import java.util.List;
import java.util.UUID;
import org.springframework.http.HttpStatus;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.ResponseStatus;
import org.springframework.web.bind.annotation.RestController;

@Tag(name = DocumentacaoDaApi.PONTOS)
@RestController
@RequestMapping("/api/v1/pontos-de-resfriamento")
public class PontosDeResfriamentoController {

    private final CadastroDePontos cadastro;

    public PontosDeResfriamentoController(CadastroDePontos cadastro) {
        this.cadastro = cadastro;
    }

    @Operation(summary = "Lista os pontos de resfriamento", description = "Ordenados pelo nome, paginados.")
    @ApiResponse(responseCode = "200", description = "A página de pontos")
    @ErrosDocumentados({400})
    @GetMapping
    public Pagina<PontoSaida> listar(
            @Parameter(example = "1") @RequestParam(defaultValue = "1") @Min(1) int pagina,
            @Parameter(example = "20") @RequestParam(defaultValue = "20") @Min(1) @Max(100) int tamanho) {
        return Pagina.de(cadastro.listar(pagina, tamanho), PontoSaida::de);
    }

    @Operation(summary = "Busca os pontos dentro de um raio, do mais perto ao mais longe",
            description = "Distância de haversine em km. Raio maior que 0 e no máximo 20 km (regra RAIO_INVALIDO).")
    @ApiResponse(responseCode = "200", description = "Os pontos dentro do raio")
    @ErrosDocumentados({400, 422})
    @GetMapping("/proximos")
    public List<PontoProximoSaida> proximos(
            @Parameter(example = "-23.4964") @RequestParam double lat,
            @Parameter(example = "-46.62") @RequestParam double lon,
            @Parameter(example = "2") @RequestParam(defaultValue = "2") double raioKm) {
        return cadastro.proximos(new Coordenada(lat, lon), raioKm).stream().map(PontoProximoSaida::de).toList();
    }

    @Operation(summary = "Consulta um ponto de resfriamento")
    @ApiResponse(responseCode = "200", description = "O ponto")
    @ErrosDocumentados({400, 404})
    @GetMapping("/{id}")
    public PontoSaida obter(@Parameter(example = "fcdcbb13-a95d-511e-951a-ea03cb9496ed") @PathVariable UUID id) {
        return PontoSaida.de(cadastro.obter(id));
    }

    @Operation(summary = "Cadastra um ponto de resfriamento",
            description = "Abre antes de fechar (HORARIO_INVALIDO), coordenada válida (COORDENADA_INVALIDA) e sempre com a fonte.")
    @ApiResponse(responseCode = "201", description = "Ponto cadastrado")
    @ErrosDocumentados({400, 422})
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public PontoSaida criar(@Valid @RequestBody PontoEntrada dados) {
        return PontoSaida.de(cadastro.criar(dados.paraDados()));
    }

    @Operation(summary = "Edita um ponto de resfriamento")
    @ApiResponse(responseCode = "200", description = "Ponto editado")
    @ErrosDocumentados({400, 404, 422})
    @PutMapping("/{id}")
    public PontoSaida atualizar(@Parameter(example = "fcdcbb13-a95d-511e-951a-ea03cb9496ed") @PathVariable UUID id,
            @Valid @RequestBody PontoEntrada dados) {
        return PontoSaida.de(cadastro.atualizar(id, dados.paraDados()));
    }

    @Operation(summary = "Remove um ponto de resfriamento")
    @ApiResponse(responseCode = "204", description = "Ponto removido")
    @ErrosDocumentados({400, 404})
    @DeleteMapping("/{id}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void remover(@Parameter(example = "fcdcbb13-a95d-511e-951a-ea03cb9496ed") @PathVariable UUID id) {
        cadastro.remover(id);
    }
}
