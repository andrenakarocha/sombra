package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.dominio.Estacao;
import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

/** Entrada e saída das regiões climáticas. O DTO valida o formato; a entidade valida a regra. */
public final class RegiaoDtos {

    private RegiaoDtos() {
    }

    @Schema(name = "NovaRegiao", description = "Uma região climática nova")
    public record NovaRegiao(
            @NotBlank @Size(max = 40) @Schema(example = "SP-MIRANTE", description = "UF, hífen e o nome, em maiúsculas")
            String codigo,
            @NotBlank @Size(max = 80) @Schema(example = "Zona Norte de São Paulo (Mirante de Santana)") String nome,
            @NotBlank @Size(max = 8) @Schema(example = "A701", description = "Código da estação do INMET de referência")
            String estacao) {
    }

    @Schema(name = "EdicaoDeRegiao", description = "Os campos editáveis de uma região climática")
    public record EdicaoDeRegiao(
            @NotBlank @Size(max = 80) @Schema(example = "Zona Norte de São Paulo (Mirante de Santana)") String nome,
            @NotBlank @Size(max = 8) @Schema(example = "A701") String estacao) {
    }

    @Schema(name = "Estacao", description = "Uma estação meteorológica automática do INMET")
    public record EstacaoSaida(
            @Schema(example = "A701") String codigo,
            @Schema(example = "São Paulo – Mirante de Santana") String nome,
            @Schema(example = "-23.496389") double latitude,
            @Schema(example = "-46.62") double longitude,
            @Schema(example = "785.64") Double altitudeM) {

        static EstacaoSaida de(Estacao e) {
            return new EstacaoSaida(e.getCodigo(), e.getNome(), e.getCoordenada().latitude(),
                    e.getCoordenada().longitude(), e.getAltitudeM());
        }
    }

    @Schema(name = "Regiao", description = "Uma região climática e a sua estação de referência")
    public record RegiaoSaida(
            @Schema(example = "SP-MIRANTE") String codigo,
            @Schema(example = "Zona Norte de São Paulo (Mirante de Santana)") String nome,
            EstacaoSaida estacao) {

        static RegiaoSaida de(RegiaoClimatica r) {
            return new RegiaoSaida(r.getCodigo(), r.getNome(), EstacaoSaida.de(r.getEstacao()));
        }
    }
}
