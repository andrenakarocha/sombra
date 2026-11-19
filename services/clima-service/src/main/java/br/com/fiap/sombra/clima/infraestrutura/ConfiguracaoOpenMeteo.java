package br.com.fiap.sombra.clima.infraestrutura;

import java.net.URI;
import java.time.Duration;
import org.springframework.boot.context.properties.ConfigurationProperties;

/**
 * A conversa com a Open-Meteo (sem chave): onde, de quanto em quanto tempo e com quanta paciência.
 *
 * @param habilitado se a coleta agendada roda (desligada nos testes)
 * @param url        o endpoint de previsão ({@code CLIMA_OPEN_METEO_URL})
 * @param intervalo  o intervalo entre coletas
 * @param timeout    o tempo máximo de conexão e de leitura de cada tentativa
 * @param tentativas quantas vezes tentar antes de desistir
 * @param espera     a espera antes da segunda tentativa (dobra a cada nova)
 */
@ConfigurationProperties(prefix = "clima.open-meteo")
public record ConfiguracaoOpenMeteo(boolean habilitado, URI url, Duration intervalo, Duration timeout, int tentativas,
        Duration espera) {
}
