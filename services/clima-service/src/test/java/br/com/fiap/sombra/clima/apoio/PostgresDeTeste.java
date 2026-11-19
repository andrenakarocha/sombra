package br.com.fiap.sombra.clima.apoio;

import org.springframework.boot.test.context.TestConfiguration;
import org.springframework.boot.testcontainers.service.connection.ServiceConnection;
import org.springframework.context.annotation.Bean;
import org.testcontainers.containers.PostgreSQLContainer;

/**
 * Um PostgreSQL 16 de verdade por contexto de teste (Testcontainers), com a mesma imagem do compose. O Flyway aplica as
 * migrações nele, como num banco recém-criado.
 */
@TestConfiguration(proxyBeanMethods = false)
public class PostgresDeTeste {

    @Bean
    @ServiceConnection
    PostgreSQLContainer<?> postgres() {
        return new PostgreSQLContainer<>("postgres:16-alpine");
    }
}
