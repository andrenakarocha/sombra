package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.api.RegiaoDtos.EdicaoDeRegiao;
import br.com.fiap.sombra.clima.api.RegiaoDtos.NovaRegiao;
import br.com.fiap.sombra.clima.api.RegiaoDtos.RegiaoSaida;
import br.com.fiap.sombra.clima.aplicacao.CadastroDeRegioes;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.validation.Valid;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
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

@Tag(name = DocumentacaoDaApi.REGIOES)
@RestController
@RequestMapping("/api/v1/regioes")
public class RegioesController {

    private final CadastroDeRegioes cadastro;

    public RegioesController(CadastroDeRegioes cadastro) {
        this.cadastro = cadastro;
    }

    @Operation(summary = "Lista as regiões climáticas", description = "Ordenadas pelo código, paginadas.")
    @ApiResponse(responseCode = "200", description = "A página de regiões")
    @ErrosDocumentados({400})
    @GetMapping
    public Pagina<RegiaoSaida> listar(
            @Parameter(example = "1") @RequestParam(defaultValue = "1") @Min(1) int pagina,
            @Parameter(example = "20") @RequestParam(defaultValue = "20") @Min(1) @Max(100) int tamanho) {
        return Pagina.de(cadastro.listar(pagina, tamanho), RegiaoSaida::de);
    }

    @Operation(summary = "Consulta uma região climática pelo código")
    @ApiResponse(responseCode = "200", description = "A região")
    @ErrosDocumentados({404})
    @GetMapping("/{codigo}")
    public RegiaoSaida obter(@Parameter(example = "SP-MIRANTE") @PathVariable String codigo) {
        return RegiaoSaida.de(cadastro.obter(codigo));
    }

    @Operation(summary = "Cadastra uma região climática", description = "A estação de referência precisa existir (regra REGIAO_SEM_ESTACAO).")
    @ApiResponse(responseCode = "201", description = "Região cadastrada")
    @ErrosDocumentados({400, 409, 422})
    @PostMapping
    @ResponseStatus(HttpStatus.CREATED)
    public RegiaoSaida criar(@Valid @RequestBody NovaRegiao dados) {
        return RegiaoSaida.de(cadastro.criar(dados.codigo(), dados.nome(), dados.estacao()));
    }

    @Operation(summary = "Edita o nome e a estação de referência de uma região")
    @ApiResponse(responseCode = "200", description = "Região editada")
    @ErrosDocumentados({400, 404, 422})
    @PutMapping("/{codigo}")
    public RegiaoSaida atualizar(@Parameter(example = "SP-MIRANTE") @PathVariable String codigo,
            @Valid @RequestBody EdicaoDeRegiao dados) {
        return RegiaoSaida.de(cadastro.atualizar(codigo, dados.nome(), dados.estacao()));
    }

    @Operation(summary = "Remove uma região climática",
            description = "As observações ficam com a estação. O Core passa a receber 404 para essa região e a trata como desconhecida.")
    @ApiResponse(responseCode = "204", description = "Região removida")
    @ErrosDocumentados({404})
    @DeleteMapping("/{codigo}")
    @ResponseStatus(HttpStatus.NO_CONTENT)
    public void remover(@Parameter(example = "SP-MIRANTE") @PathVariable String codigo) {
        cadastro.remover(codigo);
    }
}
