package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.Test;

class RegraPrevisorDeCalorTest {

    final PrevisorDeCalor regra = new RegraPrevisorDeCalor();
    final Estacao a701 = EstacaoTest.a701();

    Observacao hora(int h, double t, double ur) {
        return a701.registrar(Instant.parse("2023-11-15T00:00:00Z").plusSeconds(h * 3600L), Medicao.de(t, ur),
                FonteDaObservacao.INMET);
    }

    @Test
    void oPicoDasUltimas24HorasEOPicoPrevisto() {
        PrevisaoDeCalor p = regra.prever(List.of(hora(0, 25, 60), hora(18, 35.7, 29), hora(23, 27, 50)));

        assertThat(p.indiceDeCalorPrevistoC()).isEqualTo(35.47);
        assertThat(p.nivelPrevisto()).isEqualTo(NivelDeRisco.CAUTELA_EXTREMA);
        assertThat(p.probabilidadePerigo()).isEqualTo(0.07);
        assertThat(p.horizonteHoras()).isEqualTo(24);
        assertThat(p.fonte()).isEqualTo("regra");
        assertThat(p.modelo()).isEqualTo(RegraPrevisorDeCalor.MODELO);
    }

    @Test
    void aProbabilidadeEMeioNoLimiarDePerigoESobeAcimaDele() {
        // 35 °C e 50 % → 40,68 °C: acima do limiar de PERIGO (39,44)
        PrevisaoDeCalor perigo = regra.prever(List.of(hora(18, 35, 50)));

        assertThat(perigo.nivelPrevisto()).isEqualTo(NivelDeRisco.PERIGO);
        assertThat(perigo.probabilidadePerigo()).isGreaterThan(0.5).isLessThan(1.0);
        assertThat(regra.prever(List.of(hora(3, 20, 60))).probabilidadePerigo()).isZero();
    }

    @Test
    void janelaVaziaNaoTemPrevisao() {
        assertThatThrownBy(() -> regra.prever(List.of())).isInstanceOf(IllegalArgumentException.class);
        assertThatThrownBy(() -> regra.prever(null)).isInstanceOf(IllegalArgumentException.class);
    }

    @Test
    void aPrevisaoRecusaProbabilidadeForaDeZeroAUm() {
        assertThatThrownBy(() -> new PrevisaoDeCalor(1.2, NivelDeRisco.PERIGO, 40, 24, "m", "regra"))
                .isInstanceOf(IllegalArgumentException.class);
    }
}
