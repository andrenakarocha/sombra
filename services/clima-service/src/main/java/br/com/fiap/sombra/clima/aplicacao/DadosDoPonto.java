package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.TipoDePonto;
import java.time.LocalTime;

/** Os dados de um ponto de resfriamento na criação e na edição. */
public record DadosDoPonto(
        String nome,
        TipoDePonto tipo,
        double latitude,
        double longitude,
        LocalTime abre,
        LocalTime fecha,
        boolean acessivel,
        String fonte) {
}
