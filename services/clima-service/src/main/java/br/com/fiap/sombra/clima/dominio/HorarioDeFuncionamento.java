package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embeddable;
import java.time.LocalTime;

/**
 * O horário em que um ponto de resfriamento recebe o público, no fuso de São Paulo. Regra: abre antes de fechar (um
 * lugar que vira a noite não entra como ponto de resfriamento, porque à noite o risco é outro).
 *
 * @param abre  hora de abertura
 * @param fecha hora de fechamento
 */
@Embeddable
public record HorarioDeFuncionamento(
        @Column(name = "abre", nullable = false) LocalTime abre,
        @Column(name = "fecha", nullable = false) LocalTime fecha) {

    public HorarioDeFuncionamento {
        if (abre == null || fecha == null || !abre.isBefore(fecha)) {
            throw new RegraViolada("HORARIO_INVALIDO", "O ponto de resfriamento precisa abrir antes de fechar.");
        }
    }

    /** Se o ponto está aberto nesta hora (abre incluída, fecha excluída). */
    public boolean abertoAs(LocalTime hora) {
        return !hora.isBefore(abre) && hora.isBefore(fecha);
    }
}
