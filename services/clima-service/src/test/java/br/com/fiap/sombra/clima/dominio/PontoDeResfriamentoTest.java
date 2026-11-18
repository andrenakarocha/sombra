package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.within;

import java.time.LocalTime;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class PontoDeResfriamentoTest {

    static final HorarioDeFuncionamento COMERCIAL = new HorarioDeFuncionamento(LocalTime.of(9, 0), LocalTime.of(18, 0));

    static PontoDeResfriamento biblioteca() {
        return new PontoDeResfriamento(UUID.randomUUID(), "Biblioteca", TipoDePonto.BIBLIOTECA,
                new Coordenada(-23.5030, -46.6230), COMERCIAL, true, "OpenStreetMap");
    }

    @Test
    void abreAntesDeFechar() {
        assertThatThrownBy(() -> new HorarioDeFuncionamento(LocalTime.of(18, 0), LocalTime.of(9, 0)))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("HORARIO_INVALIDO"));
        assertThatThrownBy(() -> new HorarioDeFuncionamento(LocalTime.NOON, LocalTime.NOON)).isInstanceOf(RegraViolada.class);
        assertThatThrownBy(() -> new HorarioDeFuncionamento(null, LocalTime.NOON)).isInstanceOf(RegraViolada.class);
    }

    @Test
    void sabeSeEstaAberto() {
        assertThat(COMERCIAL.abertoAs(LocalTime.of(9, 0))).isTrue();
        assertThat(COMERCIAL.abertoAs(LocalTime.of(17, 59))).isTrue();
        assertThat(COMERCIAL.abertoAs(LocalTime.of(18, 0))).isFalse();
        assertThat(COMERCIAL.abertoAs(LocalTime.of(8, 59))).isFalse();
    }

    @Test
    void sempreDizDeOndeVeioODado() {
        assertThatThrownBy(() -> new PontoDeResfriamento(UUID.randomUUID(), "UBS", TipoDePonto.UBS,
                new Coordenada(0, 0), COMERCIAL, true, " "))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("FONTE_OBRIGATORIA"));
    }

    @Test
    void exigeNome() {
        assertThatThrownBy(() -> new PontoDeResfriamento(UUID.randomUUID(), null, TipoDePonto.UBS,
                new Coordenada(0, 0), COMERCIAL, true, "OSM"))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("NOME_OBRIGATORIO"));
    }

    @Test
    void mediADistanciaAteACasa() {
        PontoDeResfriamento ponto = biblioteca();

        assertThat(ponto.distanciaKmAte(EstacaoTest.MIRANTE)).isCloseTo(0.79, within(0.01));
        assertThat(ponto.isAcessivel()).isTrue();
        assertThat(ponto.getTipo()).isEqualTo(TipoDePonto.BIBLIOTECA);
    }

    @Test
    void atualizaComAsMesmasRegras() {
        PontoDeResfriamento ponto = biblioteca();

        ponto.atualizar(" UBS Santana ", TipoDePonto.UBS, new Coordenada(-23.5, -46.6),
                new HorarioDeFuncionamento(LocalTime.of(7, 0), LocalTime.of(19, 0)), false, " Prefeitura ");

        assertThat(ponto.getNome()).isEqualTo("UBS Santana");
        assertThat(ponto.getFonte()).isEqualTo("Prefeitura");
        assertThat(ponto.getHorario().abre()).isEqualTo(LocalTime.of(7, 0));
        assertThat(ponto.isAcessivel()).isFalse();
    }
}
