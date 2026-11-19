// Sombra · Sentinela · a forma canônica que é assinada (docs/contratos/mqtt.asyncapi.yaml, ADR-004). O Core monta a
// mesma string e confere o HMAC: T (telemetria), E (evento) e C (comando, do Core para a Sentinela).
#pragma once
#include <stdint.h>
#include <stdio.h>

namespace sombra {

inline int canonicaDaTelemetria(char* saida, size_t n, const char* deviceId, uint32_t ts, uint32_t seq, double t,
                                double rh, double hi, const char* nivel) {
  return snprintf(saida, n, "T|1|%s|%lu|%lu|%.1f|%.1f|%.1f|%s", deviceId, (unsigned long)ts, (unsigned long)seq, t, rh,
                  hi, nivel);
}

inline int canonicaDoEvento(char* saida, size_t n, const char* deviceId, uint32_t ts, uint32_t seq, const char* tipo) {
  return snprintf(saida, n, "E|1|%s|%lu|%lu|%s", deviceId, (unsigned long)ts, (unsigned long)seq, tipo);
}

inline int canonicaDoComando(char* saida, size_t n, const char* deviceId, uint32_t ts, uint32_t seq, const char* tipo,
                             const char* mensagem) {
  return snprintf(saida, n, "C|1|%s|%lu|%lu|%s|%s", deviceId, (unsigned long)ts, (unsigned long)seq, tipo, mensagem);
}

}  // namespace sombra
