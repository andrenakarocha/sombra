package br.com.fiap.sombra.clima.api;

import br.com.fiap.sombra.clima.aplicacao.DadosDoPonto;
import br.com.fiap.sombra.clima.aplicacao.PontoProximo;
import br.com.fiap.sombra.clima.dominio.PontoDeResfriamento;
import br.com.fiap.sombra.clima.dominio.TipoDePonto;
import com.fasterxml.jackson.annotation.JsonFormat;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import jakarta.validation.constraints.Size;
import java.time.LocalTime;
import java.util.UUID;

/** Entrada e saída dos pontos de resfriamento. */
public final class PontoDtos {

    private PontoDtos() {
    }

    @Schema(name = "Horario", description = "Horário de funcionamento, no fuso de São Paulo")
    public record HorarioDto(
            @NotNull @JsonFormat(pattern = "HH:mm") @Schema(type = "string", pattern = "^\\d\\d:\\d\\d$", example = "09:00")
            LocalTime abre,
            @NotNull @JsonFormat(pattern = "HH:mm") @Schema(type = "string", pattern = "^\\d\\d:\\d\\d$", example = "18:00")
            LocalTime fecha) {
    }

    @Schema(name = "DadosDoPonto", description = "Um ponto de resfriamento na criação ou na edição")
    public record PontoEntrada(
            @NotBlank @Size(max = 120) @Schema(example = "Biblioteca Municipal Nuto Sant'anna") String nome,
            @NotNull @Schema(example = "BIBLIOTECA") TipoDePonto tipo,
            @NotNull @Schema(example = "-23.498444") Double latitude,
            @NotNull @Schema(example = "-46.619591") Double longitude,
            @NotNull @Valid HorarioDto horario,
            @NotNull @Schema(example = "false", description = "true só quando a fonte confirma a acessibilidade") Boolean acessivel,
            @NotBlank @Size(max = 200) @Schema(example = "OpenStreetMap (ODbL), node/4312079136") String fonte) {

        DadosDoPonto paraDados() {
            return new DadosDoPonto(nome, tipo, latitude, longitude, horario.abre(), horario.fecha(), acessivel, fonte);
        }
    }

    @Schema(name = "PontoDeResfriamento", description = "Um lugar público e fresco para o pico do calor")
    public record PontoSaida(
            @Schema(example = "fcdcbb13-a95d-511e-951a-ea03cb9496ed") UUID id,
            @Schema(example = "Biblioteca Municipal Nuto Sant'anna") String nome,
            @Schema(example = "BIBLIOTECA") TipoDePonto tipo,
            @Schema(example = "-23.498444") double latitude,
            @Schema(example = "-46.619591") double longitude,
            HorarioDto horario,
            @Schema(example = "false") boolean acessivel,
            @Schema(example = "OpenStreetMap (ODbL), node/4312079136; horário no OSM: Mo-Fr 09:00-18:00; Sa 09:00-16:00")
            String fonte) {

        static PontoSaida de(PontoDeResfriamento p) {
            return new PontoSaida(p.getId(), p.getNome(), p.getTipo(), p.getCoordenada().latitude(),
                    p.getCoordenada().longitude(), new HorarioDto(p.getHorario().abre(), p.getHorario().fecha()),
                    p.isAcessivel(), p.getFonte());
        }
    }

    @Schema(name = "PontoProximo", description = "Um ponto de resfriamento e a distância até a origem da busca")
    public record PontoProximoSaida(
            @Schema(example = "fcdcbb13-a95d-511e-951a-ea03cb9496ed") UUID id,
            @Schema(example = "Biblioteca Municipal Nuto Sant'anna") String nome,
            @Schema(example = "BIBLIOTECA") TipoDePonto tipo,
            @Schema(example = "0.2", description = "Distância em km, com uma casa") double distanciaKm,
            HorarioDto horario,
            @Schema(example = "false") boolean acessivel,
            @Schema(example = "OpenStreetMap (ODbL), node/4312079136; horário no OSM: Mo-Fr 09:00-18:00; Sa 09:00-16:00")
            String fonte,
            @Schema(example = "-23.498444") double latitude,
            @Schema(example = "-46.619591") double longitude) {

        static PontoProximoSaida de(PontoProximo proximo) {
            PontoDeResfriamento p = proximo.ponto();
            return new PontoProximoSaida(p.getId(), p.getNome(), p.getTipo(),
                    Math.round(proximo.distanciaKm() * 10.0) / 10.0,
                    new HorarioDto(p.getHorario().abre(), p.getHorario().fecha()), p.isAcessivel(), p.getFonte(),
                    p.getCoordenada().latitude(), p.getCoordenada().longitude());
        }
    }
}
