package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Estacao;
import br.com.fiap.sombra.clima.dominio.Medicao;
import br.com.fiap.sombra.clima.dominio.Observacao;
import br.com.fiap.sombra.clima.dominio.Periodo;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import br.com.fiap.sombra.clima.infraestrutura.EstacaoRepository;
import br.com.fiap.sombra.clima.infraestrutura.ObservacaoRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** Ingestão das observações horárias de uma estação e a consulta por período. */
@Service
public class RegistroDeObservacoes {

    private static final Logger LOG = LoggerFactory.getLogger(RegistroDeObservacoes.class);

    private final EstacaoRepository estacoes;
    private final ObservacaoRepository observacoes;

    public RegistroDeObservacoes(EstacaoRepository estacoes, ObservacaoRepository observacoes) {
        this.estacoes = estacoes;
        this.observacoes = observacoes;
    }

    /** Registra uma hora medida. A mesma hora da mesma estação só entra uma vez (409 na segunda). */
    @Transactional
    public Observacao registrar(String codigoDaEstacao, NovaObservacao nova) {
        Estacao estacao = estacao(codigoDaEstacao);
        Medicao medicao;
        try {
            medicao = new Medicao(nova.temperaturaC(), nova.umidadePct(), nova.pressaoHpa(), nova.radiacaoKjM2(),
                    nova.ventoMs(), nova.precipitacaoMm());
        } catch (RegraViolada e) {
            LOG.atWarn().addKeyValue("event", "observacao.recusada").addKeyValue("estacao", estacao.getCodigo())
                    .addKeyValue("regra", e.getRegra()).log("Observação recusada pela regra {}", e.getRegra());
            throw e;
        }
        if (observacoes.existsByEstacaoCodigoAndObservadaEm(estacao.getCodigo(), nova.observadaEm())) {
            throw new RecursoJaExiste("observação da estação " + estacao.getCodigo() + " na hora", nova.observadaEm());
        }
        Observacao observacao = observacoes.save(estacao.registrar(nova.observadaEm(), medicao, nova.fonte()));
        LOG.atInfo().addKeyValue("event", "observacao.registrada").addKeyValue("estacao", estacao.getCodigo())
                .addKeyValue("observadaEm", observacao.getObservadaEm().toString())
                .addKeyValue("nivel", observacao.getNivel().name()).addKeyValue("fonte", observacao.getFonte().name())
                .log("Observação registrada");
        return observacao;
    }

    @Transactional(readOnly = true)
    public Page<Observacao> noPeriodo(String codigoDaEstacao, Periodo periodo, int pagina, int tamanho) {
        Estacao estacao = estacao(codigoDaEstacao);
        return observacoes.noPeriodo(estacao.getCodigo(), periodo.de(), periodo.ate(), PageRequest.of(pagina - 1, tamanho));
    }

    private Estacao estacao(String codigo) {
        return estacoes.findById(Codigos.normalizar(codigo)).orElseThrow(() -> new RecursoNaoEncontrado("estação", codigo));
    }
}
