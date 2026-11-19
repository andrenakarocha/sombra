package br.com.fiap.sombra.clima.aplicacao;

import java.time.Instant;

/**
 * Como está o replay de uma região.
 *
 * @param regiao       a região tocada
 * @param inicio       a hora real da onda de calor em que o replay começou
 * @param fim          a última hora com dado; ao passar dela, o replay se encerra sozinho
 * @param velocidade   horas de dado por hora real (60 = uma hora de dado por minuto)
 * @param iniciadoEm   quando o replay foi ligado, no relógio real
 * @param instanteAtual o "agora" do replay
 */
public record EstadoDoReplay(String regiao, Instant inicio, Instant fim, int velocidade, Instant iniciadoEm,
        Instant instanteAtual) {
}
