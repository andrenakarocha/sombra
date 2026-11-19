package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Coordenada;
import br.com.fiap.sombra.clima.dominio.HorarioDeFuncionamento;
import br.com.fiap.sombra.clima.dominio.PontoDeResfriamento;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import br.com.fiap.sombra.clima.infraestrutura.PontoDeResfriamentoRepository;
import java.util.List;
import java.util.UUID;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** CRUD dos pontos de resfriamento e a busca dos mais próximos de uma casa. */
@Service
public class CadastroDePontos {

    /** A busca vai até 20 km: mais longe que isso, ninguém sai de casa no pico do calor para se refrescar. */
    public static final double RAIO_MAXIMO_KM = 20.0;

    private final PontoDeResfriamentoRepository pontos;

    public CadastroDePontos(PontoDeResfriamentoRepository pontos) {
        this.pontos = pontos;
    }

    @Transactional(readOnly = true)
    public Page<PontoDeResfriamento> listar(int pagina, int tamanho) {
        return pontos.findAll(PageRequest.of(pagina - 1, tamanho, Sort.by("nome")));
    }

    @Transactional(readOnly = true)
    public PontoDeResfriamento obter(UUID id) {
        return pontos.findById(id).orElseThrow(() -> new RecursoNaoEncontrado("ponto de resfriamento", id));
    }

    @Transactional
    public PontoDeResfriamento criar(DadosDoPonto dados) {
        return pontos.save(new PontoDeResfriamento(UUID.randomUUID(), dados.nome(), dados.tipo(),
                new Coordenada(dados.latitude(), dados.longitude()), new HorarioDeFuncionamento(dados.abre(), dados.fecha()),
                dados.acessivel(), dados.fonte()));
    }

    @Transactional
    public PontoDeResfriamento atualizar(UUID id, DadosDoPonto dados) {
        PontoDeResfriamento ponto = obter(id);
        ponto.atualizar(dados.nome(), dados.tipo(), new Coordenada(dados.latitude(), dados.longitude()),
                new HorarioDeFuncionamento(dados.abre(), dados.fecha()), dados.acessivel(), dados.fonte());
        return ponto;
    }

    @Transactional
    public void remover(UUID id) {
        pontos.delete(obter(id));
    }

    @Transactional(readOnly = true)
    public List<PontoProximo> proximos(Coordenada origem, double raioKm) {
        if (!(raioKm > 0.0 && raioKm <= RAIO_MAXIMO_KM)) {
            throw new RegraViolada("RAIO_INVALIDO", "O raio da busca precisa ser maior que 0 e no máximo 20 km.");
        }
        return pontos.proximos(origem, raioKm).stream()
                .map(p -> new PontoProximo(p, p.distanciaKmAte(origem)))
                .toList();
    }
}
