package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Estacao;
import br.com.fiap.sombra.clima.dominio.FonteDaObservacao;
import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import br.com.fiap.sombra.clima.infraestrutura.FonteIndisponivel;
import br.com.fiap.sombra.clima.infraestrutura.HoraMedida;
import br.com.fiap.sombra.clima.infraestrutura.ObservacaoRepository;
import br.com.fiap.sombra.clima.infraestrutura.OpenMeteoClient;
import br.com.fiap.sombra.clima.infraestrutura.RegiaoClimaticaRepository;
import java.util.LinkedHashMap;
import java.util.Map;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;

/**
 * A cada 10 minutos, busca na Open-Meteo as últimas 24 h de cada estação de referência e grava só as horas que faltam
 * (a mesma hora nunca entra duas vezes). Se a Open-Meteo cair, registra a falha e segue com o que já tem.
 */
@Service
public class ColetaAoVivo {

    static final int HORAS = 24;

    private static final Logger LOG = LoggerFactory.getLogger(ColetaAoVivo.class);

    private final RegiaoClimaticaRepository regioes;
    private final ObservacaoRepository observacoes;
    private final RegistroDeObservacoes registro;
    private final OpenMeteoClient openMeteo;

    public ColetaAoVivo(RegiaoClimaticaRepository regioes, ObservacaoRepository observacoes,
            RegistroDeObservacoes registro, OpenMeteoClient openMeteo) {
        this.regioes = regioes;
        this.observacoes = observacoes;
        this.registro = registro;
        this.openMeteo = openMeteo;
    }

    @Scheduled(fixedDelayString = "${clima.open-meteo.intervalo}", initialDelayString = "${clima.open-meteo.atraso-inicial:PT10S}")
    void agendada() {
        coletar();
    }

    /** Coleta todas as estações de referência e devolve quantas horas novas entraram. */
    public int coletar() {
        Map<String, Estacao> estacoes = new LinkedHashMap<>();
        for (RegiaoClimatica r : regioes.findAll()) {
            estacoes.putIfAbsent(r.getEstacao().getCodigo(), r.getEstacao());
        }
        int novas = 0;
        for (Estacao estacao : estacoes.values()) {
            try {
                for (HoraMedida hora : openMeteo.ultimasHoras(estacao.getCoordenada(), HORAS)) {
                    if (!observacoes.existsByEstacaoCodigoAndObservadaEm(estacao.getCodigo(), hora.observadaEm())) {
                        var m = hora.medicao();
                        registro.registrar(estacao.getCodigo(), new NovaObservacao(hora.observadaEm(), m.temperaturaC(),
                                m.umidadePct(), m.pressaoHpa(), m.radiacaoKjM2(), m.ventoMs(), m.precipitacaoMm(),
                                FonteDaObservacao.OPEN_METEO));
                        novas++;
                    }
                }
            } catch (FonteIndisponivel | RecursoJaExiste e) {
                LOG.atWarn().addKeyValue("event", "fonte_meteorologica.falhou").addKeyValue("fonte", "open-meteo")
                        .addKeyValue("estacao", estacao.getCodigo()).addKeyValue("tipo", e.getClass().getSimpleName())
                        .log("Coleta da Open-Meteo falhou; o Clima segue com as horas que já tem");
            }
        }
        return novas;
    }
}
