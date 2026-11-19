package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.Estacao;
import org.springframework.data.jpa.repository.JpaRepository;

public interface EstacaoRepository extends JpaRepository<Estacao, String> {
}
