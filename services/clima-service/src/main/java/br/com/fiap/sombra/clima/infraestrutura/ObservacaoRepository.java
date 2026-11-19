package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.Observacao;
import java.time.Instant;
import java.util.List;
import java.util.Optional;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.Pageable;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface ObservacaoRepository extends JpaRepository<Observacao, Long> {

    /** As observações de uma estação no período [de, ate), da mais recente para a mais antiga, paginadas. */
    @Query(value = """
            select o from Observacao o
            where o.estacao.codigo = :codigo and o.observadaEm >= :de and o.observadaEm < :ate
            order by o.observadaEm desc
            """, countQuery = """
            select count(o) from Observacao o
            where o.estacao.codigo = :codigo and o.observadaEm >= :de and o.observadaEm < :ate
            """)
    Page<Observacao> noPeriodo(@Param("codigo") String codigo, @Param("de") Instant de, @Param("ate") Instant ate,
            Pageable pagina);

    /** A janela de observações (da mais antiga para a mais recente) que alimenta a previsão: (desde, ate]. */
    @Query("""
            select o from Observacao o
            where o.estacao.codigo = :codigo and o.observadaEm > :desde and o.observadaEm <= :ate
            order by o.observadaEm
            """)
    List<Observacao> janela(@Param("codigo") String codigo, @Param("desde") Instant desde, @Param("ate") Instant ate);

    /** A observação mais recente até um instante: o "agora" da estação (ao vivo ou no relógio do replay). */
    Optional<Observacao> findFirstByEstacaoCodigoAndObservadaEmLessThanEqualOrderByObservadaEmDesc(String codigo,
            Instant instante);

    boolean existsByEstacaoCodigoAndObservadaEm(String codigo, Instant observadaEm);

    /** A última hora de uma fonte numa estação: o fim do replay ({@code INMET}) ou a coleta mais recente. */
    Optional<Observacao> findFirstByEstacaoCodigoAndFonteOrderByObservadaEmDesc(String codigo, FonteDaObservacao fonte);

    /** Se há dado da fonte na janela [de, ate): o replay só começa onde há onda de calor para tocar. */
    boolean existsByEstacaoCodigoAndFonteAndObservadaEmGreaterThanEqualAndObservadaEmLessThan(String codigo,
            FonteDaObservacao fonte, Instant de, Instant ate);
}
