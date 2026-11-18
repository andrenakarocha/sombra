package br.com.fiap.sombra.clima.dominio;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

class MedicaoTest {

    private static void assertRegra(Runnable acao, String regra) {
        assertThatThrownBy(acao::run).isInstanceOfSatisfying(RegraViolada.class, e -> assertThat(e.getRegra()).isEqualTo(regra));
    }

    @Test
    void menosNoveMilNaTemperaturaOuNaUmidadeNaoEObservacao() {
        assertRegra(() -> Medicao.de(-9999, 50), "OBSERVACAO_FALTANTE");
        assertRegra(() -> Medicao.de(30, -9999), "OBSERVACAO_FALTANTE");
    }

    @Test
    void menosNoveMilNumaGrandezaOpcionalTambemNaoEObservacao() {
        assertRegra(() -> new Medicao(30, 50, -9999.0, null, null, null), "OBSERVACAO_FALTANTE");
        assertRegra(() -> new Medicao(30, 50, null, -9999.0, null, null), "OBSERVACAO_FALTANTE");
        assertRegra(() -> new Medicao(30, 50, null, null, -9999.0, null), "OBSERVACAO_FALTANTE");
        assertRegra(() -> new Medicao(30, 50, null, null, null, -9999.0), "OBSERVACAO_FALTANTE");
    }

    @ParameterizedTest
    @ValueSource(doubles = {-0.1, 100.1, Double.NaN})
    void umidadeFicaEntreZeroECem(double umidade) {
        assertRegra(() -> Medicao.de(30, umidade), "UMIDADE_FORA_DA_FAIXA");
    }

    @ParameterizedTest
    @ValueSource(doubles = {-40.1, 60.1, Double.NaN})
    void temperaturaFicaNumaFaixaFisica(double temperatura) {
        assertRegra(() -> Medicao.de(temperatura, 50), "TEMPERATURA_FORA_DA_FAIXA");
    }

    @Test
    void ventoEChuvaNaoSaoNegativos() {
        assertRegra(() -> new Medicao(30, 50, null, null, -1.0, null), "GRANDEZA_NEGATIVA");
        assertRegra(() -> new Medicao(30, 50, null, null, null, -0.2), "GRANDEZA_NEGATIVA");
    }

    @Test
    void aceitaOsExtremosDaFaixaEAsOpcionaisAusentes() {
        assertThat(Medicao.de(-40, 0).umidadePct()).isZero();
        assertThat(Medicao.de(60, 100).temperaturaC()).isEqualTo(60);
        Medicao completa = new Medicao(35.0, 50.0, 925.1, 2600.0, 1.8, 0.0);
        assertThat(completa.indiceDeCalor().arredondado()).isEqualTo(40.68);
    }
}
