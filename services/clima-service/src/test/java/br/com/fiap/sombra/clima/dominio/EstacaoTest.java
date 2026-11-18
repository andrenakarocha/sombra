package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import java.time.Instant;
import org.junit.jupiter.api.Test;

class EstacaoTest {

    static final Coordenada MIRANTE = new Coordenada(-23.4964, -46.6200);

    static Estacao a701() {
        return new Estacao("a701 ", "São Paulo – Mirante de Santana", MIRANTE, 785.64);
    }

    @Test
    void normalizaOCodigoDoInmet() {
        assertThat(a701().getCodigo()).isEqualTo("A701");
    }

    @Test
    void recusaCodigoForaDoPadraoDoInmet() {
        assertThatThrownBy(() -> new Estacao("SP-01", "x", MIRANTE, null))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("CODIGO_DE_ESTACAO_INVALIDO"));
        assertThatThrownBy(() -> new Estacao(null, "x", MIRANTE, null)).isInstanceOf(RegraViolada.class);
    }

    @Test
    void exigeNome() {
        assertThatThrownBy(() -> new Estacao("A701", " ", MIRANTE, null))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("NOME_OBRIGATORIO"));
    }

    @Test
    void registraAObservacaoComOIndiceEONivelCalculadosNoDominio() {
        Estacao estacao = a701();
        Instant hora = Instant.parse("2023-11-14T18:00:00Z");

        Observacao o = estacao.registrar(hora, Medicao.de(35.0, 50.0), FonteDaObservacao.INMET);

        assertThat(o.getEstacao()).isSameAs(estacao);
        assertThat(o.getObservadaEm()).isEqualTo(hora);
        assertThat(o.getIndiceDeCalorC()).isEqualTo(40.68);
        assertThat(o.getNivel()).isEqualTo(NivelDeRisco.PERIGO);
        assertThat(o.getFonte()).isEqualTo(FonteDaObservacao.INMET);
        assertThat(o.getMedicao().temperaturaC()).isEqualTo(35.0);
    }

    @Test
    void observacaoExigeAHora() {
        assertThatThrownBy(() -> a701().registrar(null, Medicao.de(30, 50), FonteDaObservacao.INMET))
                .isInstanceOf(NullPointerException.class);
    }
}
