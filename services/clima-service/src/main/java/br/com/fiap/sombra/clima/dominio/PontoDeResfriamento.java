package br.com.fiap.sombra.clima.dominio;

import jakarta.persistence.Column;
import jakarta.persistence.Embedded;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import java.util.Objects;
import java.util.UUID;

/**
 * Um lugar público e fresco para onde a pessoa idosa pode ir no pico do calor (biblioteca, UBS, centro de
 * convivência). Regras: tem nome, abre antes de fechar e sempre diz de onde veio o dado.
 */
@Entity
@Table(name = "ponto_de_resfriamento")
public class PontoDeResfriamento {

    @Id
    private UUID id;

    @Column(nullable = false, length = 120)
    private String nome;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false, length = 32)
    private TipoDePonto tipo;

    @Embedded
    private Coordenada coordenada;

    @Embedded
    private HorarioDeFuncionamento horario;

    @Column(nullable = false)
    private boolean acessivel;

    @Column(nullable = false, length = 200)
    private String fonte;

    protected PontoDeResfriamento() {
        // exigido pelo JPA
    }

    public PontoDeResfriamento(UUID id, String nome, TipoDePonto tipo, Coordenada coordenada,
            HorarioDeFuncionamento horario, boolean acessivel, String fonte) {
        this.id = Objects.requireNonNull(id, "id");
        atualizar(nome, tipo, coordenada, horario, acessivel, fonte);
    }

    /** Troca os dados do ponto, com as mesmas regras da criação. */
    public final void atualizar(String nome, TipoDePonto tipo, Coordenada coordenada,
            HorarioDeFuncionamento horario, boolean acessivel, String fonte) {
        if (nome == null || nome.isBlank()) {
            throw new RegraViolada("NOME_OBRIGATORIO", "O ponto de resfriamento precisa de um nome.");
        }
        if (fonte == null || fonte.isBlank()) {
            throw new RegraViolada("FONTE_OBRIGATORIA", "Todo ponto de resfriamento diz de onde veio o dado.");
        }
        this.nome = nome.trim();
        this.tipo = Objects.requireNonNull(tipo, "tipo");
        this.coordenada = Objects.requireNonNull(coordenada, "coordenada");
        this.horario = Objects.requireNonNull(horario, "horario");
        this.acessivel = acessivel;
        this.fonte = fonte.trim();
    }

    /** A distância em km até um ponto (haversine). */
    public double distanciaKmAte(Coordenada origem) {
        return coordenada.distanciaKmAte(origem);
    }

    public UUID getId() {
        return id;
    }

    public String getNome() {
        return nome;
    }

    public TipoDePonto getTipo() {
        return tipo;
    }

    public Coordenada getCoordenada() {
        return coordenada;
    }

    public HorarioDeFuncionamento getHorario() {
        return horario;
    }

    public boolean isAcessivel() {
        return acessivel;
    }

    public String getFonte() {
        return fonte;
    }
}
