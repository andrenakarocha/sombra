package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embedded;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.Objects;

/**
 * O que uma estação mediu numa hora, com o índice de calor e o nível de risco calculados na criação (nunca vindos de
 * fora). Nasce pela {@link Estacao#registrar}.
 */
@Entity
@Table(name = "observacao")
public class Observacao {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(optional = false, fetch = FetchType.LAZY)
    @JoinColumn(name = "estacao_codigo", nullable = false)
    private Estacao estacao;

    @Column(name = "observada_em", nullable = false)
    private Instant observadaEm;

    @Embedded
    private Medicao medicao;

    @Column(name = "indice_de_calor_c", nullable = false)
    private double indiceDeCalorC;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 16)
    private NivelDeRisco nivel;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 12)
    private FonteDaObservacao fonte;

    protected Observacao() {
        // exigido pelo JPA
    }

    Observacao(Estacao estacao, Instant observadaEm, Medicao medicao, FonteDaObservacao fonte) {
        this.estacao = Objects.requireNonNull(estacao, "estacao");
        this.observadaEm = Objects.requireNonNull(observadaEm, "observadaEm");
        this.medicao = Objects.requireNonNull(medicao, "medicao");
        this.fonte = Objects.requireNonNull(fonte, "fonte");
        IndiceDeCalor indice = medicao.indiceDeCalor();
        this.indiceDeCalorC = indice.arredondado();
        this.nivel = indice.nivel();
    }

    public Long getId() {
        return id;
    }

    public Estacao getEstacao() {
        return estacao;
    }

    public Instant getObservadaEm() {
        return observadaEm;
    }

    public Medicao getMedicao() {
        return medicao;
    }

    public double getIndiceDeCalorC() {
        return indiceDeCalorC;
    }

    public NivelDeRisco getNivel() {
        return nivel;
    }

    public FonteDaObservacao getFonte() {
        return fonte;
    }
}
