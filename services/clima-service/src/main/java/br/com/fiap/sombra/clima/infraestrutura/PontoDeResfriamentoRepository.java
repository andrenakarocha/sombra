package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.Coordenada;
import br.com.fiap.sombra.clima.dominio.PontoDeResfriamento;
import java.util.List;
import java.util.UUID;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

public interface PontoDeResfriamentoRepository extends JpaRepository<PontoDeResfriamento, UUID> {

    /** Quilômetros por grau de latitude (círculo máximo com o raio médio da Terra). */
    double KM_POR_GRAU = Math.PI * Coordenada.RAIO_DA_TERRA_KM / 180.0;

    /**
     * Os pontos dentro do raio, do mais perto ao mais longe. O banco filtra por uma caixa de latitude e longitude (usa
     * o índice) e depois pela distância de haversine, a mesma fórmula do domínio.
     */
    default List<PontoDeResfriamento> proximos(Coordenada origem, double raioKm) {
        double dLat = raioKm / KM_POR_GRAU;
        double dLon = raioKm / (KM_POR_GRAU * Math.max(Math.cos(Math.toRadians(origem.latitude())), 1e-6));
        return dentroDoRaio(origem.latitude(), origem.longitude(), raioKm,
                origem.latitude() - dLat, origem.latitude() + dLat, origem.longitude() - dLon, origem.longitude() + dLon);
    }

    @Query(nativeQuery = true, value = """
            select p.* from ponto_de_resfriamento p
            where p.latitude between :latMin and :latMax
              and p.longitude between :lonMin and :lonMax
              and 2 * 6371.0088 * asin(least(1, sqrt(
                    power(sin(radians(p.latitude - :lat) / 2), 2)
                    + cos(radians(:lat)) * cos(radians(p.latitude)) * power(sin(radians(p.longitude - :lon) / 2), 2)
                  ))) <= :raioKm
            order by 2 * 6371.0088 * asin(least(1, sqrt(
                    power(sin(radians(p.latitude - :lat) / 2), 2)
                    + cos(radians(:lat)) * cos(radians(p.latitude)) * power(sin(radians(p.longitude - :lon) / 2), 2)
                  ))), p.nome
            """)
    List<PontoDeResfriamento> dentroDoRaio(@Param("lat") double lat, @Param("lon") double lon,
            @Param("raioKm") double raioKm, @Param("latMin") double latMin, @Param("latMax") double latMax,
            @Param("lonMin") double lonMin, @Param("lonMax") double lonMax);
}
