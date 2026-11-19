package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Observacao;
import br.com.fiap.sombra.clima.dominio.PrevisaoDeCalor;
import br.com.fiap.sombra.clima.dominio.PrevisorDeCalor;
import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import br.com.fiap.sombra.clima.infraestrutura.ObservacaoRepository;
import java.time.Duration;
import java.time.Instant;
import java.util.List;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/**
 * O risco de calor de uma região, no "agora" da fonte dela (ao vivo ou replay): a observação mais recente da estação de
 * referência e a previsão de 24 h feita sobre a janela das últimas 24 h.
 */
@Service
public class ConsultaDeRiscoDeCalor {

    /** Uma observação mais velha que isso não é "agora". */
    public static final Duration VALIDADE = Duration.ofHours(3);
    static final Duration JANELA = Duration.ofHours(24);

    private static final Logger LOG = LoggerFactory.getLogger(ConsultaDeRiscoDeCalor.class);

    private final CadastroDeRegioes regioes;
    private final FontesMeteorologicas fontes;
    private final ObservacaoRepository observacoes;
    private final PrevisorDeCalor previsor;

    public ConsultaDeRiscoDeCalor(CadastroDeRegioes regioes, FontesMeteorologicas fontes,
            ObservacaoRepository observacoes, PrevisorDeCalor previsor) {
        this.regioes = regioes;
        this.fontes = fontes;
        this.observacoes = observacoes;
        this.previsor = previsor;
    }

    @Transactional(readOnly = true)
    public RiscoDeCalor consultar(String codigoDaRegiao) {
        RegiaoClimatica regiao = regioes.obter(codigoDaRegiao);
        String estacao = regiao.getEstacao().getCodigo();
        FonteMeteorologica fonte = fontes.para(regiao.getCodigo());
        Instant instante = fonte.agora();

        Observacao agora = observacoes.findFirstByEstacaoCodigoAndObservadaEmLessThanEqualOrderByObservadaEmDesc(estacao, instante)
                .filter(o -> !o.getObservadaEm().isBefore(instante.minus(VALIDADE)))
                .orElseThrow(() -> new SemObservacaoRecente(regiao.getCodigo(), estacao));
        List<Observacao> janela = observacoes.janela(estacao, instante.minus(JANELA), instante);
        PrevisaoDeCalor previsao = previsor.prever(janela);

        LOG.atInfo().addKeyValue("event", "previsao.calculada").addKeyValue("regiao", regiao.getCodigo())
                .addKeyValue("fonte", previsao.fonte()).addKeyValue("modelo", previsao.modelo())
                .addKeyValue("fonteDados", fonte.nome()).addKeyValue("nivelPrevisto", previsao.nivelPrevisto().name())
                .addKeyValue("probabilidadePerigo", previsao.probabilidadePerigo())
                .log("Previsão de 24 h calculada");
        return new RiscoDeCalor(regiao.getCodigo(), agora, previsao, fonte.nome());
    }
}
