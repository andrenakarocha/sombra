package br.com.fiap.sombra.clima;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

/** Clima Service do Sombra: o dono do conhecimento climático (regiões, observações, risco de calor e pontos de resfriamento). */
@SpringBootApplication
public class ClimaServiceApplication {

    public static void main(String[] args) {
        SpringApplication.run(ClimaServiceApplication.class, args);
    }
}
