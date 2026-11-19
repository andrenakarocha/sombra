// Sombra · Sentinela · pinagem, rede e tempos (spec 04). O segredo fica em secrets.h.
#pragma once

// pinos (ESP32 DevKit v1)
#define PINO_DHT 15
#define PINO_LED_R 25
#define PINO_LED_G 26
#define PINO_LED_B 27
#define PINO_BUZZER 4
#define PINO_BOTAO_VERDE 18
#define PINO_BOTAO_VERMELHO 19
#define OLED_ENDERECO 0x3C

// MQTT: o broker público do Wokwi (P9). Por isso cada mensagem é assinada e nenhuma leva dado pessoal (ADR-004).
#define MQTT_HOST "broker.hivemq.com"
#define MQTT_PORTA 1883
#define MQTT_PREFIXO "sombra/v1"

// tempo: NTP e o fuso de São Paulo (sem horário de verão)
#define NTP_SERVIDOR "pool.ntp.org"
#define FUSO "<-03>3"
#define NTP_RESSINCRONIA_MS 60000UL  // acerta o relógio a cada minuto (o padrão do SNTP é 1 h)

// cadência
#define LEITURA_MS 2000UL
#define TELEMETRIA_MS 10000UL
#define PRESSAO_LONGA_MS 2000UL
#define QR_NO_BOOT_MS 8000UL
#define JANELA_ANTI_REPLAY_S 300
