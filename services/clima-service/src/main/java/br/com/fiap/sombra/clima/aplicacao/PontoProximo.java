package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.PontoDeResfriamento;

/** Um ponto de resfriamento e a distância dele até a origem da busca. */
public record PontoProximo(PontoDeResfriamento ponto, double distanciaKm) {
}
