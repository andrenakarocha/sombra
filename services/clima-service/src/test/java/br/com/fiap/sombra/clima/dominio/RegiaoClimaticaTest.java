package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;

class RegiaoClimaticaTest {

    @Test
    void normalizaOCodigoEGuardaAEstacaoDeReferencia() {
        Estacao a701 = EstacaoTest.a701();

        RegiaoClimatica regiao = new RegiaoClimatica(" sp-mirante", " Mirante de Santana ", a701);

        assertThat(regiao.getCodigo()).isEqualTo("SP-MIRANTE");
        assertThat(regiao.getNome()).isEqualTo("Mirante de Santana");
        assertThat(regiao.getEstacao()).isSameAs(a701);
    }

    @Test
    void exigeUmaEstacaoValida() {
        assertThatThrownBy(() -> new RegiaoClimatica("SP-MIRANTE", "Mirante", null))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("REGIAO_SEM_ESTACAO"));
    }

    @Test
    void recusaCodigoSemUf() {
        assertThatThrownBy(() -> new RegiaoClimatica("MIRANTE", "Mirante", EstacaoTest.a701()))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("CODIGO_DE_REGIAO_INVALIDO"));
        assertThatThrownBy(() -> new RegiaoClimatica(null, "Mirante", EstacaoTest.a701())).isInstanceOf(RegraViolada.class);
    }

    @Test
    void atualizarSegueAsMesmasRegras() {
        RegiaoClimatica regiao = new RegiaoClimatica("SP-MIRANTE", "Mirante", EstacaoTest.a701());

        assertThatThrownBy(() -> regiao.atualizar("", EstacaoTest.a701()))
                .isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo("NOME_OBRIGATORIO"));
        assertThatThrownBy(() -> regiao.atualizar("Santana", null)).isInstanceOf(RegraViolada.class);

        regiao.atualizar("Santana", EstacaoTest.a701());
        assertThat(regiao.getNome()).isEqualTo("Santana");
    }
}
