package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;
import java.util.Locale;
import java.util.regex.Pattern;

/**
 * A área atendida por uma estação de referência (ex.: SP-MIRANTE). O Core guarda só o código da região de cada
 * domicílio e pergunta o risco ao Clima: Região N—1 Estação. Regra: toda região tem uma estação válida.
 */
@Entity
@Table(name = "regiao_climatica")
public class RegiaoClimatica {

    private static final Pattern CODIGO = Pattern.compile("[A-Z]{2}-[A-Z0-9]+(-[A-Z0-9]+)*");

    @Id
    @Column(length = 40)
    private String codigo;

    @Column(nullable = false, length = 80)
    private String nome;

    @ManyToOne(optional = false, fetch = FetchType.EAGER)
    @JoinColumn(name = "estacao_codigo", nullable = false)
    private Estacao estacao;

    protected RegiaoClimatica() {
        // exigido pelo JPA
    }

    public RegiaoClimatica(String codigo, String nome, Estacao estacao) {
        String normalizado = codigo == null ? "" : codigo.trim().toUpperCase(Locale.ROOT);
        if (!CODIGO.matcher(normalizado).matches()) {
            throw new RegraViolada("CODIGO_DE_REGIAO_INVALIDO",
                    "O código da região começa pela UF e um hífen, em maiúsculas (ex.: SP-MIRANTE).");
        }
        this.codigo = normalizado;
        atualizar(nome, estacao);
    }

    /** Troca o nome e a estação de referência, com as mesmas regras da criação. */
    public final void atualizar(String nome, Estacao estacao) {
        if (nome == null || nome.isBlank()) {
            throw new RegraViolada("NOME_OBRIGATORIO", "A região precisa de um nome.");
        }
        if (estacao == null) {
            throw new RegraViolada("REGIAO_SEM_ESTACAO", "Uma região climática precisa de uma estação de referência válida.");
        }
        this.nome = nome.trim();
        this.estacao = estacao;
    }

    public String getCodigo() {
        return codigo;
    }

    public String getNome() {
        return nome;
    }

    public Estacao getEstacao() {
        return estacao;
    }
}
