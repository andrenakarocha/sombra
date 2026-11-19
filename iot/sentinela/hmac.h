// Sombra · Sentinela · SHA-256 e HMAC-SHA256 portáteis (FIPS 180-4 e RFC 2104), sem dependência: o mesmo código roda no
// ESP32 e nos testes do host contra os vetores de docs/contratos/mqtt-assinatura.vetores.json. Toda mensagem leva o
// HMAC da forma canônica (ADR-004): o Core recusa a que não bate, e ninguém falsifica um "estou bem".
#pragma once
#include <stddef.h>
#include <stdint.h>
#include <string.h>

namespace sombra {

class Sha256 {
 public:
  Sha256() { reiniciar(); }

  void reiniciar() {
    static const uint32_t iniciais[8] = {0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
                                         0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19};
    memcpy(h_, iniciais, sizeof h_);
    tamanho_ = 0;
    usado_ = 0;
  }

  void atualizar(const uint8_t* dados, size_t n) {
    for (size_t i = 0; i < n; i++) {
      bloco_[usado_++] = dados[i];
      if (usado_ == 64) {
        processar(bloco_);
        usado_ = 0;
      }
    }
    tamanho_ += n;
  }

  void finalizar(uint8_t saida[32]) {
    const uint64_t bits = tamanho_ * 8;
    const uint8_t um = 0x80, zero = 0x00;
    atualizar(&um, 1);
    while (usado_ != 56) atualizar(&zero, 1);
    uint8_t tamanho[8];
    for (int i = 0; i < 8; i++) tamanho[i] = static_cast<uint8_t>(bits >> (56 - 8 * i));
    atualizar(tamanho, 8);
    for (int i = 0; i < 8; i++) {
      saida[4 * i] = static_cast<uint8_t>(h_[i] >> 24);
      saida[4 * i + 1] = static_cast<uint8_t>(h_[i] >> 16);
      saida[4 * i + 2] = static_cast<uint8_t>(h_[i] >> 8);
      saida[4 * i + 3] = static_cast<uint8_t>(h_[i]);
    }
  }

 private:
  static uint32_t rotr(uint32_t x, int n) { return (x >> n) | (x << (32 - n)); }

  void processar(const uint8_t* b) {
    static const uint32_t k[64] = {
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2};
    uint32_t w[64];
    for (int i = 0; i < 16; i++) {
      w[i] = (uint32_t)b[4 * i] << 24 | (uint32_t)b[4 * i + 1] << 16 | (uint32_t)b[4 * i + 2] << 8 | b[4 * i + 3];
    }
    for (int i = 16; i < 64; i++) {
      const uint32_t s0 = rotr(w[i - 15], 7) ^ rotr(w[i - 15], 18) ^ (w[i - 15] >> 3);
      const uint32_t s1 = rotr(w[i - 2], 17) ^ rotr(w[i - 2], 19) ^ (w[i - 2] >> 10);
      w[i] = w[i - 16] + s0 + w[i - 7] + s1;
    }
    uint32_t a = h_[0], bb = h_[1], c = h_[2], d = h_[3], e = h_[4], f = h_[5], g = h_[6], hh = h_[7];
    for (int i = 0; i < 64; i++) {
      const uint32_t t1 = hh + (rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25)) + ((e & f) ^ (~e & g)) + k[i] + w[i];
      const uint32_t t2 = (rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22)) + ((a & bb) ^ (a & c) ^ (bb & c));
      hh = g; g = f; f = e; e = d + t1; d = c; c = bb; bb = a; a = t1 + t2;
    }
    h_[0] += a; h_[1] += bb; h_[2] += c; h_[3] += d; h_[4] += e; h_[5] += f; h_[6] += g; h_[7] += hh;
  }

  uint32_t h_[8];
  uint8_t bloco_[64];
  uint64_t tamanho_;
  size_t usado_;
};

// HMAC-SHA256 de uma mensagem com a chave (32 bytes); a saída em hexadecimal minúsculo (64 caracteres + '\0').
inline void hmacSha256Hex(const uint8_t* chave, size_t tamanhoDaChave, const char* mensagem, char saida[65]) {
  uint8_t k[64] = {0}, ipad[64], opad[64], interno[32], mac[32];
  if (tamanhoDaChave > 64) {
    Sha256 s;
    s.atualizar(chave, tamanhoDaChave);
    s.finalizar(k);
  } else {
    memcpy(k, chave, tamanhoDaChave);
  }
  for (int i = 0; i < 64; i++) {
    ipad[i] = k[i] ^ 0x36;
    opad[i] = k[i] ^ 0x5c;
  }
  Sha256 s;
  s.atualizar(ipad, 64);
  s.atualizar(reinterpret_cast<const uint8_t*>(mensagem), strlen(mensagem));
  s.finalizar(interno);
  s.reiniciar();
  s.atualizar(opad, 64);
  s.atualizar(interno, 32);
  s.finalizar(mac);
  static const char hex[] = "0123456789abcdef";
  for (int i = 0; i < 32; i++) {
    saida[2 * i] = hex[mac[i] >> 4];
    saida[2 * i + 1] = hex[mac[i] & 0x0f];
  }
  saida[64] = '\0';
}

// O segredo da Sentinela chega em hexadecimal (64 caracteres = 32 bytes).
inline bool hexParaBytes(const char* hex, uint8_t* saida, size_t n) {
  auto valor = [](char c) -> int {
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
  };
  for (size_t i = 0; i < n; i++) {
    const int alto = valor(hex[2 * i]), baixo = valor(hex[2 * i + 1]);
    if (alto < 0 || baixo < 0) return false;
    saida[i] = static_cast<uint8_t>(alto << 4 | baixo);
  }
  return true;
}

}  // namespace sombra
