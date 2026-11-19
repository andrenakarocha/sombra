package br.com.fiap.sombra.clima.infraestrutura;

import br.com.fiap.sombra.clima.dominio.Coordenada;
import br.com.fiap.sombra.clima.dominio.Medicao;
import br.com.fiap.sombra.clima.dominio.RegraViolada;
import com.fasterxml.jackson.annotation.JsonProperty;
import java.time.Clock;
import java.time.Instant;
import java.time.LocalDateTime;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.client.SimpleClientHttpRequestFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;
import org.springframework.web.util.UriComponentsBuilder;

/**
 * Cliente da Open-Meteo (sem chave, P7 conferida em 2026-11-18): as últimas horas de temperatura, umidade, pressão,
 * radiação, vento e chuva num ponto. Resiliente: timeout por tentativa e novas tentativas com espera crescente; quando
 * desiste, lança {@link FonteIndisponivel} e quem chama segue com o último dado que já tem.
 */
@Component
public class OpenMeteoClient {

    private static final Logger LOG = LoggerFactory.getLogger(OpenMeteoClient.class);

    /** A Open-Meteo dá a radiação em W/m² (média da hora); o INMET, em kJ/m² (acumulado da hora): × 3600 s ÷ 1000. */
    static final double W_M2_PARA_KJ_M2_POR_HORA = 3.6;

    private static final String VARIAVEIS =
            "temperature_2m,relative_humidity_2m,surface_pressure,shortwave_radiation,wind_speed_10m,precipitation";

    private final ConfiguracaoOpenMeteo configuracao;
    private final RestClient http;
    private final Clock relogio;

    public OpenMeteoClient(ConfiguracaoOpenMeteo configuracao, RestClient.Builder builder, Clock relogio) {
        this.configuracao = configuracao;
        this.relogio = relogio;
        SimpleClientHttpRequestFactory fabrica = new SimpleClientHttpRequestFactory();
        fabrica.setConnectTimeout(configuracao.timeout());
        fabrica.setReadTimeout(configuracao.timeout());
        this.http = builder.requestFactory(fabrica).defaultHeader("User-Agent", "sombra-clima-service/1.0").build();
    }

    /** As horas já medidas (até a hora cheia atual) das últimas {@code horas} horas, da mais antiga à mais recente. */
    public List<HoraMedida> ultimasHoras(Coordenada ponto, int horas) {
        Resposta resposta = comTentativas(ponto, horas);
        Instant agora = relogio.instant().truncatedTo(ChronoUnit.HOURS);
        List<HoraMedida> medidas = new ArrayList<>();
        Horaria h = resposta.hourly();
        for (int i = 0; i < h.time().size(); i++) {
            Instant hora = LocalDateTime.parse(h.time().get(i)).toInstant(ZoneOffset.UTC);
            Double t = h.temperatura().get(i);
            Double ur = h.umidade().get(i);
            if (hora.isAfter(agora) || t == null || ur == null) {
                continue;
            }
            Double radiacao = h.radiacao().get(i) == null ? null : h.radiacao().get(i) * W_M2_PARA_KJ_M2_POR_HORA;
            try {
                medidas.add(new HoraMedida(hora, new Medicao(t, ur, h.pressao().get(i), radiacao, h.vento().get(i),
                        h.precipitacao().get(i))));
            } catch (RegraViolada e) {
                LOG.atWarn().addKeyValue("event", "observacao.recusada").addKeyValue("fonte", "open-meteo")
                        .addKeyValue("regra", e.getRegra()).log("Hora da Open-Meteo recusada pela regra {}", e.getRegra());
            }
        }
        return medidas;
    }

    private Resposta comTentativas(Coordenada ponto, int horas) {
        var uri = UriComponentsBuilder.fromUri(configuracao.url())
                .queryParam("latitude", String.format(Locale.ROOT, "%.4f", ponto.latitude()))
                .queryParam("longitude", String.format(Locale.ROOT, "%.4f", ponto.longitude()))
                .queryParam("hourly", VARIAVEIS)
                .queryParam("past_hours", horas)
                .queryParam("forecast_hours", 1)
                .queryParam("timezone", "GMT")
                .queryParam("wind_speed_unit", "ms")
                .build().toUri();
        RestClientException ultima = null;
        long espera = configuracao.espera().toMillis();
        for (int tentativa = 1; tentativa <= configuracao.tentativas(); tentativa++) {
            try {
                Resposta resposta = http.get().uri(uri).retrieve().body(Resposta.class);
                if (resposta == null || resposta.hourly() == null) {
                    throw new RestClientException("resposta sem a série horária");
                }
                return resposta;
            } catch (RestClientException e) {
                ultima = e;
                if (tentativa < configuracao.tentativas()) {
                    LOG.atWarn().addKeyValue("event", "fonte_meteorologica.falhou").addKeyValue("fonte", "open-meteo")
                            .addKeyValue("tentativa", tentativa).addKeyValue("tipo", e.getClass().getSimpleName())
                            .log("Open-Meteo falhou; nova tentativa em {} ms", espera);
                    dormir(espera);
                    espera *= 2;
                }
            }
        }
        throw new FonteIndisponivel("A Open-Meteo não respondeu em " + configuracao.tentativas() + " tentativas.", ultima);
    }

    private static void dormir(long ms) {
        try {
            Thread.sleep(ms);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }

    record Resposta(Horaria hourly) {
    }

    record Horaria(
            List<String> time,
            @JsonProperty("temperature_2m") List<Double> temperatura,
            @JsonProperty("relative_humidity_2m") List<Double> umidade,
            @JsonProperty("surface_pressure") List<Double> pressao,
            @JsonProperty("shortwave_radiation") List<Double> radiacao,
            @JsonProperty("wind_speed_10m") List<Double> vento,
            @JsonProperty("precipitation") List<Double> precipitacao) {
    }
}
