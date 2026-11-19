package br.com.fiap.sombra.clima.infraestrutura;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.assertj.core.api.Assertions.within;

import br.com.fiap.sombra.clima.apoio.PostgresDeTeste;
import br.com.fiap.sombra.clima.dominio.Coordenada;
import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.IndiceDeCalor;
import br.com.fiap.sombra.clima.dominio.Medicao;
import br.com.fiap.sombra.clima.dominio.NivelDeRisco;
import br.com.fiap.sombra.clima.dominio.Observacao;
import br.com.fiap.sombra.clima.dominio.PontoDeResfriamento;
import java.time.Instant;
import java.util.List;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.autoconfigure.jdbc.AutoConfigureTestDatabase;
import org.springframework.boot.test.autoconfigure.orm.jpa.DataJpaTest;
import org.springframework.context.annotation.Import;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.jdbc.core.JdbcTemplate;

/** Migrações num banco vazio, o seed real e as consultas além do CRUD, contra um PostgreSQL de verdade. */
@DataJpaTest
@AutoConfigureTestDatabase(replace = AutoConfigureTestDatabase.Replace.NONE)
@Import(PostgresDeTeste.class)
class RepositoriosTest {

    static final Coordenada MIRANTE = new Coordenada(-23.4964, -46.6200);

    @Autowired
    EstacaoRepository estacoes;

    @Autowired
    RegiaoClimaticaRepository regioes;

    @Autowired
    ObservacaoRepository observacoes;

    @Autowired
    PontoDeResfriamentoRepository pontos;

    @Autowired
    JdbcTemplate jdbc;

    @Test
    void asQuatroMigracoesRodamNumBancoVazioEOHibernateSoConfere() {
        List<String> versoes = jdbc.queryForList(
                "select version from flyway_schema_history where success order by installed_rank", String.class);

        assertThat(versoes).containsExactly("1", "2", "3", "4");
    }

    @Test
    void oSeedTrazARegiaoAEstacaoEAOndaDeCalorRealDeNovembroDe2023() {
        assertThat(regioes.findById("SP-MIRANTE")).hasValueSatisfying(r ->
                assertThat(r.getEstacao().getCodigo()).isEqualTo("A701"));
        assertThat(estacoes.findById("A701")).hasValueSatisfying(e ->
                assertThat(e.getCoordenada().distanciaKmAte(MIRANTE)).isLessThan(0.01));
        assertThat(observacoes.count()).isEqualTo(637);
        assertThat(pontos.count()).isEqualTo(9);
    }

    @Test
    void oIndiceENivelDoSeedBatemComODominioEmTodasAsHoras() {
        List<Observacao> todas = observacoes.findAll();

        assertThat(todas).allSatisfy(o -> {
            IndiceDeCalor esperado = o.getMedicao().indiceDeCalor();
            assertThat(o.getIndiceDeCalorC()).isCloseTo(esperado.celsius(), within(0.006));
            assertThat(o.getNivel()).isEqualTo(esperado.nivel());
        });
        assertThat(todas).extracting(Observacao::getIndiceDeCalorC).contains(35.47);
    }

    @Test
    void consultaOPeriodoPaginadoDaMaisRecenteParaAMaisAntiga() {
        Instant de = Instant.parse("2023-11-15T00:00:00Z");
        Instant ate = Instant.parse("2023-11-16T00:00:00Z");

        Page<Observacao> primeira = observacoes.noPeriodo("A701", de, ate, PageRequest.of(0, 10));
        Page<Observacao> ultima = observacoes.noPeriodo("A701", de, ate, PageRequest.of(2, 10));

        assertThat(primeira.getTotalElements()).isEqualTo(24);
        assertThat(primeira.getTotalPages()).isEqualTo(3);
        assertThat(primeira.getContent().getFirst().getObservadaEm()).isEqualTo(Instant.parse("2023-11-15T23:00:00Z"));
        assertThat(ultima.getContent()).hasSize(4);
        assertThat(ultima.getContent().getLast().getObservadaEm()).isEqualTo(de);
    }

    @Test
    void achaOAgoraEAJanelaDeVinteEQuatroHoras() {
        Instant pico = Instant.parse("2023-11-15T18:30:00Z");

        assertThat(observacoes.findFirstByEstacaoCodigoAndObservadaEmLessThanEqualOrderByObservadaEmDesc("A701", pico))
                .hasValueSatisfying(o -> {
                    assertThat(o.getObservadaEm()).isEqualTo(Instant.parse("2023-11-15T18:00:00Z"));
                    assertThat(o.getNivel()).isEqualTo(NivelDeRisco.CAUTELA_EXTREMA);
                });
        List<Observacao> janela = observacoes.janela("A701", pico.minusSeconds(24 * 3600), pico);
        assertThat(janela).hasSize(24);
        assertThat(janela.getFirst().getObservadaEm()).isBefore(janela.getLast().getObservadaEm());
    }

    @Test
    void buscaOsPontosDentroDoRaioDoMaisPertoAoMaisLonge() {
        List<PontoDeResfriamento> perto = pontos.proximos(MIRANTE, 1.0);

        assertThat(perto).extracting(PontoDeResfriamento::getNome)
                .containsExactly("Biblioteca Municipal Nuto Sant'anna", "Santana Shopping");
        assertThat(perto.getFirst().distanciaKmAte(MIRANTE)).isCloseTo(0.23, within(0.01));
        assertThat(pontos.proximos(MIRANTE, 5.0)).hasSize(9)
                .extracting(p -> p.distanciaKmAte(MIRANTE)).isSorted();
    }

    @Test
    void aChaveEstrangeiraRecusaObservacaoDeEstacaoInexistente() {
        assertThatThrownBy(() -> jdbc.update("""
                insert into observacao (estacao_codigo, observada_em, temperatura_c, umidade_pct, indice_de_calor_c, nivel, fonte)
                values ('Z999', now(), 30, 50, 31, 'ATENCAO', 'INMET')"""))
                .isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    void umaHoraSoEntraUmaVezPorEstacao() {
        Instant hora = Instant.parse("2023-11-15T18:00:00Z");
        assertThat(observacoes.existsByEstacaoCodigoAndObservadaEm("A701", hora)).isTrue();

        Observacao repetida = estacoes.findById("A701").orElseThrow()
                .registrar(hora, Medicao.de(30, 50), FonteDaObservacao.INMET);
        assertThatThrownBy(() -> observacoes.saveAndFlush(repetida)).isInstanceOf(DataIntegrityViolationException.class);
    }

    @Test
    void oBancoTambemExigeQueOPontoAbraAntesDeFechar() {
        assertThatThrownBy(() -> jdbc.update("""
                insert into ponto_de_resfriamento (id, nome, tipo, latitude, longitude, acessivel, abre, fecha, fonte)
                values (gen_random_uuid(), 'x', 'UBS', 0, 0, false, '18:00', '09:00', 'teste')"""))
                .isInstanceOf(DataIntegrityViolationException.class);
    }
}
