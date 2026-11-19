package br.com.fiap.sombra.clima.aplicacao;

import br.com.fiap.sombra.clima.dominio.Estacao;
import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import br.com.fiap.sombra.clima.infraestrutura.EstacaoRepository;
import br.com.fiap.sombra.clima.infraestrutura.RegiaoClimaticaRepository;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

/** CRUD das regiões climáticas. A regra "toda região tem uma estação válida" vale na criação e na edição. */
@Service
public class CadastroDeRegioes {

    private final RegiaoClimaticaRepository regioes;
    private final EstacaoRepository estacoes;

    public CadastroDeRegioes(RegiaoClimaticaRepository regioes, EstacaoRepository estacoes) {
        this.regioes = regioes;
        this.estacoes = estacoes;
    }

    @Transactional(readOnly = true)
    public Page<RegiaoClimatica> listar(int pagina, int tamanho) {
        return regioes.findAll(PageRequest.of(pagina - 1, tamanho, Sort.by("codigo")));
    }

    @Transactional(readOnly = true)
    public RegiaoClimatica obter(String codigo) {
        return regioes.findById(Codigos.normalizar(codigo))
                .orElseThrow(() -> new RecursoNaoEncontrado("região climática", codigo));
    }

    @Transactional
    public RegiaoClimatica criar(String codigo, String nome, String codigoDaEstacao) {
        if (regioes.existsById(Codigos.normalizar(codigo))) {
            throw new RecursoJaExiste("região climática", Codigos.normalizar(codigo));
        }
        return regioes.save(new RegiaoClimatica(codigo, nome, estacaoValida(codigoDaEstacao)));
    }

    @Transactional
    public RegiaoClimatica atualizar(String codigo, String nome, String codigoDaEstacao) {
        RegiaoClimatica regiao = obter(codigo);
        regiao.atualizar(nome, estacaoValida(codigoDaEstacao));
        return regiao;
    }

    @Transactional
    public void remover(String codigo) {
        regioes.delete(obter(codigo));
    }

    private Estacao estacaoValida(String codigo) {
        return estacoes.findById(Codigos.normalizar(codigo)).orElseThrow(() -> new RegraViolada("REGIAO_SEM_ESTACAO",
                "A estação '" + codigo + "' não existe: uma região climática precisa de uma estação de referência válida."));
    }
}
