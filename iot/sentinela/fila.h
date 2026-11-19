// Sombra · Sentinela · a fila de eventos sem rede (buffer circular). Cada evento entra já assinado, com o ts e o seq do
// momento em que aconteceu, e sai na ordem quando o MQTT volta. O Core só aceita mensagens de até 5 min (a janela
// anti-replay do ADR-004); um evento mais velho que isso sai da fila sem ser enviado, em vez de virar um falso "replay" no
// dashboard de segurança. Cheia, a fila descarta o mais antigo.
#pragma once
#include <stddef.h>
#include <stdint.h>
#include <string.h>

namespace sombra {

template <size_t CAPACIDADE, size_t TAMANHO>
class Fila {
 public:
  void colocar(uint32_t ts, const char* mensagem) {
    if (quantos_ == CAPACIDADE) {
      avancar();
      descartados_++;
    }
    Item& item = itens_[(inicio_ + quantos_) % CAPACIDADE];
    item.ts = ts;
    strncpy(item.mensagem, mensagem, TAMANHO - 1);
    item.mensagem[TAMANHO - 1] = '\0';
    quantos_++;
  }

  // Tira do começo da fila o que o Core já não aceitaria. Devolve quantos saíram.
  size_t descartarExpirados(uint32_t agora, uint32_t janela) {
    size_t expirados = 0;
    while (quantos_ && agora > itens_[inicio_].ts && agora - itens_[inicio_].ts > janela) {
      avancar();
      expirados++;
    }
    expirados_ += expirados;
    return expirados;
  }

  const char* primeiro() const { return quantos_ ? itens_[inicio_].mensagem : nullptr; }

  void retirar() {
    if (quantos_) avancar();
  }

  size_t quantos() const { return quantos_; }
  size_t descartados() const { return descartados_; }
  size_t expirados() const { return expirados_; }

 private:
  struct Item {
    uint32_t ts;
    char mensagem[TAMANHO];
  };

  void avancar() {
    inicio_ = (inicio_ + 1) % CAPACIDADE;
    quantos_--;
  }

  Item itens_[CAPACIDADE] = {};
  size_t inicio_ = 0;
  size_t quantos_ = 0;
  size_t descartados_ = 0;
  size_t expirados_ = 0;
};

}  // namespace sombra
