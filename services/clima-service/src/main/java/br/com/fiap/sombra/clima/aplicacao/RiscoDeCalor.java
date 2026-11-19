package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Observacao;
import br.com.fiap.sombra.clima.dominio.PrevisaoDeCalor;

/** O risco de calor de uma região: a observação mais recente, a previsão de 24 h e de qual fonte veio o dado. */
public record RiscoDeCalor(String regiao, Observacao agora, PrevisaoDeCalor previsao24h, String fonteDados) {
}
