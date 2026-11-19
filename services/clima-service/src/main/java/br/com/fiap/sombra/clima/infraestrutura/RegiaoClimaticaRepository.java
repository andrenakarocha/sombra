package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.RegiaoClimatica;
import org.springframework.data.jpa.repository.JpaRepository;

public interface RegiaoClimaticaRepository extends JpaRepository<RegiaoClimatica, String> {
}
