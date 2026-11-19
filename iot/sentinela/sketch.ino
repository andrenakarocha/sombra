// Sombra · Sentinela (ESP32 no Wokwi): a porta física do Sombra na casa da pessoa idosa.
// Mede o calor de dentro da casa (DHT22), calcula o índice de calor aqui mesmo, conversa por luz fixa, 3 bipes curtos,
// tela e dois botões grandes, e fala com o Core por MQTT assinado (HMAC-SHA256), nos dois sentidos. Sem internet, ela
// continua alertando e guarda os eventos para quando a rede voltar. A lógica (índice, estados, assinatura, fila) está nos
// headers e tem testes no host: testes/testar.cpp.
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <ArduinoJson.h>
#include <DHTesp.h>
#include <Preferences.h>
#include <PubSubClient.h>
#include <WiFi.h>
#include <Wire.h>
#include <qrcode.h>  // o esp_qrcode do ESP-IDF, que já vem no core do ESP32 (sem biblioteca extra)
#include <time.h>

#include "config.h"
#include "contrato.h"
#include "estado.h"
#include "fila.h"
#include "hmac.h"
#include "indice_calor.h"
#include "secrets.h"

using namespace sombra;

Adafruit_SSD1306 oled(128, 64, &Wire, -1);
DHTesp dht;
WiFiClient wifi;
PubSubClient mqtt(wifi);
Preferences nvs;
Estado estado;
Fila<16, 256> fila;

uint8_t segredo[32];
uint32_t seq = 0;               // anti-replay: sempre crescente, guardado na NVS
uint32_t ultimoSeqDoCore = 0;   // o último comando aceito, para recusar repetição
unsigned long ultimaLeitura = 0, ultimaTelemetria = 0, inicioVermelho = 0, fimDoQr = 0;
bool vermelhoDisparou = false, verdeAntes = false, bootEnviado = false;
char topico[64];

// ── log serial legível (observabilidade §5): [NIVEL] evento chave=valor ───────────────────────────────────────────────
void logSerial(const char* nivel, const char* evento, const char* detalhes = "") {
  Serial.printf("[%s] %s %s\n", nivel, evento, detalhes);
}

uint32_t agora() { return static_cast<uint32_t>(time(nullptr)); }
bool relogioCerto() { return agora() > 1700000000UL; }  // depois do NTP
int horaLocal() {
  struct tm t;
  return getLocalTime(&t, 0) ? t.tm_hour : 12;
}

// A mesma regra do Core para os comandos (Sentinela.ProximoSeqDeComando): o maior entre o último mais um e os segundos
// Unix de agora. Continua crescente mesmo se a flash for apagada, como o Wokwi faz a cada simulação. Só é chamado com o
// relógio já acertado pelo NTP. Como o relógio já garante o crescimento, a NVS guarda o seq a cada 60 mensagens (uns
// 10 min), e não a cada uma: são ~150 gravações por dia em vez de ~8.600, o que poupa a flash de uma Sentinela real.
uint32_t proximoSeq() {
  static uint8_t desdeAGravacao = 60;  // grava já na primeira mensagem depois do boot
  seq = max(seq + 1, agora());
  if (++desdeAGravacao >= 60) {
    nvs.putUInt("seq", seq);
    desdeAGravacao = 0;
  }
  return seq;
}

const char* topicoDe(const char* canal) {
  snprintf(topico, sizeof topico, "%s/%s/%s", MQTT_PREFIXO, DEVICE_ID, canal);
  return topico;
}

// ── mensagens assinadas ───────────────────────────────────────────────────────────────────────────────────────────────
void publicarTelemetria() {
  if (!mqtt.connected() || !relogioCerto()) return;
  const uint32_t ts = agora(), s = proximoSeq();
  char canonica[160], sig[65];
  canonicaDaTelemetria(canonica, sizeof canonica, DEVICE_ID, ts, s, estado.t(), estado.rh(), estado.hi(),
                       nomeDoNivel(estado.nivel()));
  hmacSha256Hex(segredo, 32, canonica, sig);
  char t[8], rh[8], hi[8], corpo[256];
  snprintf(t, sizeof t, "%.1f", estado.t());
  snprintf(rh, sizeof rh, "%.1f", estado.rh());
  snprintf(hi, sizeof hi, "%.1f", estado.hi());
  JsonDocument doc;
  doc["v"] = 1;
  doc["ts"] = ts;
  doc["seq"] = s;
  doc["t"] = serialized(t);
  doc["rh"] = serialized(rh);
  doc["hi"] = serialized(hi);
  doc["nivel"] = nomeDoNivel(estado.nivel());
  doc["sig"] = sig;
  serializeJson(doc, corpo, sizeof corpo);
  mqtt.publish(topicoDe("telemetria"), corpo);
  char detalhes[80];
  snprintf(detalhes, sizeof detalhes, "hi=%.1f nivel=%s seq=%lu", estado.hi(), nomeDoNivel(estado.nivel()), (unsigned long)s);
  logSerial("INFO", "telemetria.enviada", detalhes);
}

// Um evento é assinado na hora em que acontece e vai para a fila: sai já, se houver rede, ou quando ela voltar.
void registrarEvento(const char* tipo) {
  if (!relogioCerto()) {
    logSerial("WARN", "evento.sem_relogio", tipo);
    return;
  }
  const uint32_t ts = agora(), s = proximoSeq();
  char canonica[96], sig[65], corpo[256];
  canonicaDoEvento(canonica, sizeof canonica, DEVICE_ID, ts, s, tipo);
  hmacSha256Hex(segredo, 32, canonica, sig);
  JsonDocument doc;
  doc["v"] = 1;
  doc["ts"] = ts;
  doc["seq"] = s;
  doc["tipo"] = tipo;
  doc["sig"] = sig;
  serializeJson(doc, corpo, sizeof corpo);
  fila.colocar(ts, corpo);
  char detalhes[64];
  snprintf(detalhes, sizeof detalhes, "tipo=%s seq=%lu fila=%u", tipo, (unsigned long)s, (unsigned)fila.quantos());
  logSerial("INFO", "evento.registrado", detalhes);
}

void descarregarFila() {
  if (!mqtt.connected()) return;
  const size_t expirados = fila.descartarExpirados(agora(), JANELA_ANTI_REPLAY_S - 30);  // 30 s de folga para o relógio
  if (expirados) {
    char detalhes[48];
    snprintf(detalhes, sizeof detalhes, "expirados=%u (o Core recusaria)", static_cast<unsigned>(expirados));
    logSerial("WARN", "fila.expirada", detalhes);
  }
  unsigned enviados = 0;
  while (mqtt.connected() && fila.primeiro()) {
    if (!mqtt.publish(topicoDe("evento"), fila.primeiro())) break;
    fila.retirar();
    enviados++;
  }
  if (enviados) {
    char detalhes[32];
    snprintf(detalhes, sizeof detalhes, "enviados=%u", enviados);
    logSerial("INFO", "fila.descarregada", detalhes);
  }
}

// Um comando do Core: só vale com a assinatura certa, seq novo e ts dentro de 5 min (anti-replay).
void aoReceber(char* canal, byte* bytes, unsigned int tamanho) {
  JsonDocument doc;
  if (deserializeJson(doc, bytes, tamanho)) return;
  const uint32_t ts = doc["ts"] | 0UL, s = doc["seq"] | 0UL;
  const char* tipo = doc["tipo"] | "";
  const char* mensagem = doc["mensagem"] | "";
  const char* sig = doc["sig"] | "";
  char canonica[200], esperado[65];
  canonicaDoComando(canonica, sizeof canonica, DEVICE_ID, ts, s, tipo, mensagem);
  hmacSha256Hex(segredo, 32, canonica, esperado);
  const long desvio = static_cast<long>(agora()) - static_cast<long>(ts);
  if (strcmp(sig, esperado) != 0 || s <= ultimoSeqDoCore || labs(desvio) > JANELA_ANTI_REPLAY_S) {
    logSerial("WARN", "comando.recusado", tipo);
    return;
  }
  ultimoSeqDoCore = s;
  nvs.putUInt("seqCore", s);
  estado.comando(tipo, mensagem, agora(), horaLocal());
  logSerial("INFO", "comando.aceito", tipo);
  (void)canal;
}

// ── rede ──────────────────────────────────────────────────────────────────────────────────────────────────────────────
void manterRede() {
  if (WiFi.status() != WL_CONNECTED) return;
  if (mqtt.connected()) {
    mqtt.loop();
    return;
  }
  static unsigned long ultimaTentativa = 0;
  if (millis() - ultimaTentativa < 3000) return;
  ultimaTentativa = millis();
  // LWT: se a Sentinela cair, o broker avisa o Core com "offline" (retido)
  if (mqtt.connect(DEVICE_ID, topicoDe("status"), 1, true, "offline")) {
    mqtt.publish(topicoDe("status"), "online", true);
    mqtt.subscribe(topicoDe("comando"), 1);
    logSerial("INFO", "mqtt.conectado", MQTT_HOST);
    if (!bootEnviado && relogioCerto()) {
      registrarEvento("BOOT");
      bootEnviado = true;
    }
    descarregarFila();
  } else {
    logSerial("WARN", "mqtt.desconectado", "nova tentativa em 3 s");
  }
}

// ── saídas: luz fixa, 3 bipes, tela ──────────────────────────────────────────────────────────────────────────────────
void acender(Luz luz) {
  int r = 0, g = 0, b = 0;
  switch (luz) {
    case Luz::AMARELA: r = 255; g = 170; break;
    case Luz::LARANJA: r = 255; g = 60; break;
    case Luz::VERMELHA: r = 255; break;
    case Luz::APAGADA: break;
  }
  analogWrite(PINO_LED_R, r);
  analogWrite(PINO_LED_G, g);
  analogWrite(PINO_LED_B, b);
}

void tresBipes() {
  for (int i = 0; i < 3; i++) {
    tone(PINO_BUZZER, 1800, 150);
    delay(300);
  }
}

// A fonte do OLED é CP437: troca os acentos que ela tem e tira os que ela não tem.
void imprimir(const char* texto) {
  for (const unsigned char* p = reinterpret_cast<const unsigned char*>(texto); *p; p++) {
    if (*p == 0xC3 && p[1]) {
      const unsigned char c = *++p;
      switch (c) {
        case 0xA1: oled.write(160); break;  // á
        case 0xA9: oled.write(130); break;  // é
        case 0xAD: oled.write(161); break;  // í
        case 0xB3: oled.write(162); break;  // ó
        case 0xBA: oled.write(163); break;  // ú
        case 0xA7: oled.write(135); break;  // ç
        case 0xA2: oled.write(131); break;  // â
        case 0xAA: oled.write(136); break;  // ê
        case 0xB4: oled.write(147); break;  // ô
        case 0xA3: oled.write('a'); break;  // ã
        case 0xB5: oled.write('o'); break;  // õ
        default: oled.write('?'); break;
      }
    } else {
      oled.write(*p);
    }
  }
}

void desenharTela() {
  Tela t;
  estado.tela(t, agora());
  oled.clearDisplay();
  oled.setTextColor(SSD1306_WHITE);
  oled.setTextSize(2);
  oled.setCursor(0, 0);
  imprimir(t.titulo);
  oled.setTextSize(1);
  for (int i = 0; i < 3; i++) {
    oled.setCursor(0, 22 + i * 11);
    imprimir(t.linha[i]);
  }
  if (!mqtt.connected()) {  // offline-first: ela continua funcionando, e diz que está sem rede
    oled.setCursor(0, 56);
    oled.printf("sem rede, fila %u", static_cast<unsigned>(fila.quantos()));
  }
  oled.display();
}

// Desenha o QR gerado pelo esp_qrcode do próprio core, no maior tamanho que cabe nos 64 px de altura.
void desenharQr(esp_qrcode_handle_t qr) {
  const int lado = esp_qrcode_get_size(qr);
  const int escala = lado * 2 <= 64 ? 2 : 1;
  const int topo = (64 - lado * escala) / 2;
  for (int y = 0; y < lado; y++)
    for (int x = 0; x < lado; x++)
      if (esp_qrcode_get_module(qr, x, y)) oled.fillRect(2 + x * escala, topo + y * escala, escala, escala, SSD1306_WHITE);
}

// O QR de vínculo no boot (D17): o app lê e o Core confere o código antes de entregar o segredo.
void mostrarQr() {
  char url[96];
  snprintf(url, sizeof url, "sombra://sentinela/vincular?v=1&d=%s&c=%s", DEVICE_ID, CODIGO_DE_VINCULO);
  oled.clearDisplay();
  esp_qrcode_config_t qr = {};
  qr.display_func = desenharQr;
  qr.max_qrcode_version = 4;
  qr.qrcode_ecc_level = ESP_QRCODE_ECC_LOW;
  if (esp_qrcode_generate(&qr, url) != ESP_OK) logSerial("WARN", "qr.falhou", url);
  oled.setTextSize(1);
  oled.setCursor(66, 8);
  oled.print("Vincular");
  oled.setCursor(66, 24);
  oled.print(DEVICE_ID);
  oled.setCursor(66, 40);
  oled.print(CODIGO_DE_VINCULO);
  oled.display();
}

void setup() {
  Serial.begin(115200);
  pinMode(PINO_BOTAO_VERDE, INPUT_PULLUP);
  pinMode(PINO_BOTAO_VERMELHO, INPUT_PULLUP);
  pinMode(PINO_LED_R, OUTPUT);
  pinMode(PINO_LED_G, OUTPUT);
  pinMode(PINO_LED_B, OUTPUT);
  acender(Luz::APAGADA);
  dht.setup(PINO_DHT, DHTesp::DHT22);
  oled.begin(SSD1306_SWITCHCAPVCC, OLED_ENDERECO);
  oled.cp437(true);
  hexParaBytes(SEGREDO_HEX, segredo, 32);
  nvs.begin("sombra", false);
  seq = nvs.getUInt("seq", 0);
  ultimoSeqDoCore = nvs.getUInt("seqCore", 0);
  logSerial("INFO", "boot", DEVICE_ID);
  mostrarQr();
  fimDoQr = millis() + QR_NO_BOOT_MS;
  WiFi.begin(WIFI_SSID, WIFI_SENHA, 6);
  configTzTime(FUSO, NTP_SERVIDOR);
  mqtt.setServer(MQTT_HOST, MQTT_PORTA);
  mqtt.setBufferSize(512);
  mqtt.setCallback(aoReceber);
}

void loop() {
  manterRede();

  // botões: verde num toque; vermelho só com pressão longa (2 s), contra toque acidental
  const bool verde = digitalRead(PINO_BOTAO_VERDE) == LOW;
  if (verde && !verdeAntes) registrarEvento(estado.botaoVerde(agora()));
  verdeAntes = verde;
  if (digitalRead(PINO_BOTAO_VERMELHO) == LOW) {
    if (!inicioVermelho) inicioVermelho = millis();
    if (!vermelhoDisparou && millis() - inicioVermelho >= PRESSAO_LONGA_MS) {
      registrarEvento(estado.botaoVermelhoLongo(agora()));
      vermelhoDisparou = true;
    }
  } else {
    inicioVermelho = 0;
    vermelhoDisparou = false;
  }

  if (millis() - ultimaLeitura >= LEITURA_MS) {
    ultimaLeitura = millis();
    const TempAndHumidity m = dht.getTempAndHumidity();
    if (!isnan(m.temperature) && !isnan(m.humidity)) {
      const Nivel antes = estado.nivel();
      if (estado.leitura(m.temperature, m.humidity, agora(), horaLocal())) {
        char detalhes[64];
        snprintf(detalhes, sizeof detalhes, "de=%s para=%s", nomeDoNivel(antes), nomeDoNivel(estado.nivel()));
        logSerial("INFO", "nivel.mudou", detalhes);
        publicarTelemetria();  // mudou de nível: não espera os 10 s
        ultimaTelemetria = millis();
      }
    }
  }
  if (millis() - ultimaTelemetria >= TELEMETRIA_MS) {
    ultimaTelemetria = millis();
    publicarTelemetria();
  }

  estado.tique(agora(), horaLocal());
  if (estado.consumirBip()) tresBipes();
  descarregarFila();
  acender(estado.luz());
  if (millis() > fimDoQr) desenharTela();
  delay(50);
}
