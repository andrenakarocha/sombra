package br.com.fiap.sombra.clima.infraestrutura;

import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.net.InetSocketAddress;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.ArrayDeque;
import java.util.Deque;
import java.util.concurrent.atomic.AtomicInteger;

/** Um servidor HTTP de verdade no lugar da Open-Meteo: responde o que o teste enfileirar (status e corpo). */
public final class OpenMeteoFalsa implements AutoCloseable {

    public static final String DUAS_HORAS = """
            {"hourly": {
              "time": ["2026-11-18T16:00", "2026-11-18T17:00", "2026-11-18T18:00", "2026-11-18T19:00"],
              "temperature_2m": [33.0, 35.0, null, 30.0],
              "relative_humidity_2m": [40, 50, 55, 60],
              "surface_pressure": [920.1, 919.8, 919.5, 919.9],
              "shortwave_radiation": [700.0, 500.0, 0.0, 0.0],
              "wind_speed_10m": [2.1, 1.5, 1.0, 0.8],
              "precipitation": [0.0, 0.0, 0.0, 0.0]
            }}""";

    private record Resposta(int status, String corpo) {
    }

    private final HttpServer servidor;
    private final Deque<Resposta> fila = new ArrayDeque<>();
    private final AtomicInteger chamadas = new AtomicInteger();
    private volatile String ultimaConsulta;

    public OpenMeteoFalsa() {
        try {
            servidor = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
        servidor.createContext("/v1/forecast", troca -> {
            chamadas.incrementAndGet();
            ultimaConsulta = troca.getRequestURI().getQuery();
            Resposta r;
            synchronized (fila) {
                r = fila.isEmpty() ? new Resposta(200, DUAS_HORAS) : fila.poll();
            }
            byte[] corpo = r.corpo().getBytes(StandardCharsets.UTF_8);
            troca.getResponseHeaders().add("Content-Type", "application/json");
            troca.sendResponseHeaders(r.status(), corpo.length);
            troca.getResponseBody().write(corpo);
            troca.close();
        });
        servidor.start();
    }

    public URI url() {
        return URI.create("http://127.0.0.1:" + servidor.getAddress().getPort() + "/v1/forecast");
    }

    public OpenMeteoFalsa responder(int status, String corpo) {
        synchronized (fila) {
            fila.add(new Resposta(status, corpo));
        }
        return this;
    }

    public int chamadas() {
        return chamadas.get();
    }

    public String ultimaConsulta() {
        return ultimaConsulta;
    }

    @Override
    public void close() {
        servidor.stop(0);
    }
}
