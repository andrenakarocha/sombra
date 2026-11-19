package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import br.com.fiap.sombra.clima.infraestrutura.ObservacaoRepository;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Optional;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

/**
 * O modo de demonstração: toca as horas reais do INMET (o seed de nov/2023 da A701) num relógio acelerado. Com
 * velocidade 60, cada minuto real avança uma hora de dado. Uma região por vez; passou da última hora com dado, o replay
 * se encerra sozinho e a região volta ao ao vivo.
 */
@Service
public class ReplayInmet implements FonteMeteorologica {

    private static final Logger LOG = LoggerFactory.getLogger(ReplayInmet.class);

    private final Clock relogio;
    private final CadastroDeRegioes regioes;
    private final ObservacaoRepository observacoes;

    private volatile Sessao sessao;

    public ReplayInmet(Clock relogio, CadastroDeRegioes regioes, ObservacaoRepository observacoes) {
        this.relogio = relogio;
        this.regioes = regioes;
        this.observacoes = observacoes;
    }

    @Override
    public String nome() {
        return "replay-inmet";
    }

    /** O "agora" do replay ativo; sem replay, o relógio real. */
    @Override
    public Instant agora() {
        return estado().map(EstadoDoReplay::instanteAtual).orElseGet(relogio::instant);
    }

    /** Liga o replay de uma região a partir de uma hora real do INMET. Um replay ativo é trocado pelo novo. */
    public EstadoDoReplay iniciar(String codigoDaRegiao, Instant inicio, int velocidade) {
        RegiaoClimatica regiao = regioes.obter(codigoDaRegiao);
        String estacao = regiao.getEstacao().getCodigo();
        boolean temDado = observacoes.existsByEstacaoCodigoAndFonteAndObservadaEmGreaterThanEqualAndObservadaEmLessThan(
                estacao, FonteDaObservacao.INMET, inicio, inicio.plus(Duration.ofDays(1)));
        Instant fim = observacoes.findFirstByEstacaoCodigoAndFonteOrderByObservadaEmDesc(estacao, FonteDaObservacao.INMET)
                .map(o -> o.getObservadaEm()).orElse(null);
        if (!temDado || fim == null) {
            throw new RegraViolada("REPLAY_SEM_DADOS",
                    "Não há horas do INMET da estação " + estacao + " nas 24 h a partir de " + inicio + ".");
        }
        sessao = new Sessao(regiao.getCodigo(), inicio, fim, velocidade, relogio.instant());
        LOG.atInfo().addKeyValue("event", "replay.iniciado").addKeyValue("regiao", regiao.getCodigo())
                .addKeyValue("inicio", inicio.toString()).addKeyValue("velocidade", velocidade)
                .log("Replay da onda de calor iniciado");
        return estadoDe(sessao);
    }

    /** Desliga o replay (idempotente). */
    public void encerrar() {
        Sessao atual = sessao;
        if (atual != null) {
            sessao = null;
            registrarFim(atual, "pedido");
        }
    }

    /** O estado do replay, se houver um ativo. */
    public Optional<EstadoDoReplay> estado() {
        Sessao atual = sessao;
        if (atual == null) {
            return Optional.empty();
        }
        EstadoDoReplay estado = estadoDe(atual);
        if (estado.instanteAtual().isAfter(atual.fim())) {
            sessao = null;
            registrarFim(atual, "fim_dos_dados");
            return Optional.empty();
        }
        return Optional.of(estado);
    }

    /** Se a região está em replay agora. */
    public boolean ativoPara(String codigoDaRegiao) {
        return estado().map(e -> e.regiao().equals(Codigos.normalizar(codigoDaRegiao))).orElse(false);
    }

    private EstadoDoReplay estadoDe(Sessao s) {
        Duration decorrido = Duration.between(s.iniciadoEm(), relogio.instant());
        Instant atual = s.inicio().plus(decorrido.multipliedBy(s.velocidade())).truncatedTo(ChronoUnit.SECONDS);
        return new EstadoDoReplay(s.regiao(), s.inicio(), s.fim(), s.velocidade(), s.iniciadoEm(), atual);
    }

    private static void registrarFim(Sessao s, String motivo) {
        LOG.atInfo().addKeyValue("event", "replay.encerrado").addKeyValue("regiao", s.regiao())
                .addKeyValue("motivo", motivo).log("Replay encerrado");
    }

    private record Sessao(String regiao, Instant inicio, Instant fim, int velocidade, Instant iniciadoEm) {
    }
}
