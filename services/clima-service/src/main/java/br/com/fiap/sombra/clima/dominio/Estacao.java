package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embedded;
import jakarta.persistence.Entity;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.time.Instant;
import java.util.Locale;
import java.util.Objects;
import java.util.regex.Pattern;

/**
 * Uma estação meteorológica automática do INMET (ex.: A701, São Paulo – Mirante de Santana). Ela registra as
 * observações horárias: Estação 1—N Observação.
 */
@Entity
@Table(name = "estacao")
public class Estacao {

    private static final Pattern CODIGO_INMET = Pattern.compile("[A-Z]\\d{3}");

    @Id
    @Column(length = 8)
    private String codigo;

    @Column(nullable = false, length = 80)
    private String nome;

    @Embedded
    private Coordenada coordenada;

    @Column(name = "altitude_m")
    private Double altitudeM;

    protected Estacao() {
        // exigido pelo JPA
    }

    public Estacao(String codigo, String nome, Coordenada coordenada, Double altitudeM) {
        String normalizado = codigo == null ? "" : codigo.trim().toUpperCase(Locale.ROOT);
        if (!CODIGO_INMET.matcher(normalizado).matches()) {
            throw new RegraViolada("CODIGO_DE_ESTACAO_INVALIDO",
                    "O código de estação automática do INMET tem uma letra e três dígitos (ex.: A701).");
        }
        if (nome == null || nome.isBlank()) {
            throw new RegraViolada("NOME_OBRIGATORIO", "A estação precisa de um nome.");
        }
        this.codigo = normalizado;
        this.nome = nome.trim();
        this.coordenada = Objects.requireNonNull(coordenada, "coordenada");
        this.altitudeM = altitudeM;
    }

    /** Registra o que a estação mediu numa hora, já com o índice de calor e o nível de risco. */
    public Observacao registrar(Instant observadaEm, Medicao medicao, FonteDaObservacao fonte) {
        return new Observacao(this, observadaEm, medicao, fonte);
    }

    public String getCodigo() {
        return codigo;
    }

    public String getNome() {
        return nome;
    }

    public Coordenada getCoordenada() {
        return coordenada;
    }

    public Double getAltitudeM() {
        return altitudeM;
    }
}
