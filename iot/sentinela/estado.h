// Sombra · Sentinela · a máquina de estados local, sem dependência do Arduino (testada no host em testes/testar.cpp).
// Funciona sem internet: o nível vem do índice de calor calculado aqui, e a Sentinela pede o "estou bem", explica o
// próximo passo e avisa com luz e som. Discreta por padrão (D13, pedido da entrevista): luz apagada no normal, luz fixa
// (nunca pisca) só em risco, 3 bipes curtos, e à noite (22h–7h) só o perigo faz barulho.
#pragma once
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "indice_calor.h"

namespace sombra {

enum class Luz { APAGADA, AMARELA, LARANJA, VERMELHA };
enum class Modo { OCIOSO, AGUARDANDO_CHECKIN, CONFIRMADO, AVISANDO_FAMILIA, PEDIU_AJUDA, RECADO };

// O que a tela mostra: um título grande (até 10 letras) e até 3 linhas pequenas (até 21 letras).
struct Tela {
  char titulo[12];
  char linha[3][22];
};

class Estado {
 public:
  static const uint32_t PRAZO_CAUTELA_S = 30 * 60;
  static const uint32_t PRAZO_PERIGO_S = 15 * 60;
  static const uint32_t REPETIR_BIP_PERIGO_S = 10 * 60;
  static const uint32_t REPETIR_BIP_EXTREMO_S = 5 * 60;
  static const uint32_t DURACAO_RECADO_S = 60;
  static const uint32_t DURACAO_OBRIGADO_S = 10;

  // Uma leitura do DHT22. Devolve true quando o nível mudou (a telemetria sai na hora, sem esperar os 10 s).
  bool leitura(double t, double rh, uint32_t agoraS, int horaLocal) {
    t_ = umaCasa(t);
    rh_ = umaCasa(rh);
    hi_ = umaCasa(indiceDeCalorC(t, rh));
    const Nivel antes = nivel_;
    nivel_ = classificar(hi_);
    if (nivel_ == antes) return false;
    const bool subiuParaRisco = nivel_ >= Nivel::CAUTELA_EXTREMA && nivel_ > antes;
    if (subiuParaRisco && (modo_ == Modo::OCIOSO || modo_ == Modo::RECADO || modo_ == Modo::CONFIRMADO ||
                           modo_ == Modo::AGUARDANDO_CHECKIN)) {
      pedirCheckin(agoraS);
      if (nivel_ >= Nivel::PERIGO || !noite(horaLocal)) bipar(agoraS);
    }
    return true;
  }

  // Botão verde: "estou bem". Serve para o alerta e para o check-in diário.
  const char* botaoVerde(uint32_t agoraS) {
    modo_ = Modo::CONFIRMADO;
    desde_ = agoraS;
    return "CHECKIN_ESTOU_BEM";
  }

  // Botão vermelho com pressão longa (2 s, contra toque acidental): vai direto ao degrau 3.
  const char* botaoVermelhoLongo(uint32_t agoraS) {
    modo_ = Modo::PEDIU_AJUDA;
    desde_ = agoraS;
    bipar(agoraS);
    return "PEDIDO_DE_AJUDA";
  }

  // Um comando do Core, já com a assinatura conferida.
  void comando(const char* tipo, const char* mensagem, uint32_t agoraS, int horaLocal) {
    copiar(recado_, mensagem, sizeof recado_);
    if (strcmp(tipo, "FAMILIA_AVISADA") == 0) {
      modo_ = Modo::AVISANDO_FAMILIA;
      desde_ = agoraS;
    } else if (strcmp(tipo, "ALERTA") == 0 || strcmp(tipo, "CHECKIN_DIARIO") == 0) {
      pedirCheckin(agoraS);
      if (!noite(horaLocal) || nivel_ >= Nivel::PERIGO) bipar(agoraS);
    } else if (strcmp(tipo, "NORMAL") == 0) {
      modo_ = Modo::OCIOSO;
    } else {  // PRE_ALERTA e LEMBRETE_AGUA: um recado na tela, luz amarela fixa, sem pedir nada
      copiar(tipoDoRecado_, tipo, sizeof tipoDoRecado_);
      if (modo_ == Modo::OCIOSO || modo_ == Modo::CONFIRMADO || modo_ == Modo::RECADO) {
        modo_ = Modo::RECADO;
        desde_ = agoraS;
      }
    }
  }

  // A passagem do tempo: prazo do check-in, repetição do bipe e o fim dos recados.
  void tique(uint32_t agoraS, int horaLocal) {
    if (modo_ == Modo::AGUARDANDO_CHECKIN) {
      if (agoraS >= prazo_) {
        modo_ = Modo::AVISANDO_FAMILIA;
        desde_ = agoraS;
        recado_[0] = '\0';
      } else if (nivel_ >= Nivel::PERIGO) {
        const uint32_t repetir = nivel_ == Nivel::PERIGO_EXTREMO ? REPETIR_BIP_EXTREMO_S : REPETIR_BIP_PERIGO_S;
        if (agoraS - ultimoBip_ >= repetir) bipar(agoraS);
      }
    } else if ((modo_ == Modo::RECADO && agoraS - desde_ >= DURACAO_RECADO_S) ||
               (modo_ == Modo::CONFIRMADO && agoraS - desde_ >= DURACAO_OBRIGADO_S)) {
      modo_ = Modo::OCIOSO;
    }
    (void)horaLocal;
  }

  // O bipe pendente (3 bipes curtos): quem toca o buzzer consome o pedido.
  bool consumirBip() {
    const bool b = bipPendente_;
    bipPendente_ = false;
    return b;
  }

  Luz luz() const {
    if (modo_ == Modo::PEDIU_AJUDA || nivel_ >= Nivel::PERIGO) return Luz::VERMELHA;
    if (nivel_ == Nivel::CAUTELA_EXTREMA || modo_ == Modo::AGUARDANDO_CHECKIN) return Luz::LARANJA;
    if (modo_ == Modo::RECADO) return Luz::AMARELA;
    return Luz::APAGADA;
  }

  // O que mostrar, sempre com o próximo passo explicado.
  void tela(Tela& t, uint32_t agoraS) const {
    memset(&t, 0, sizeof t);
    char medida[22];
    snprintf(medida, sizeof medida, "%.1fC %.0f%% sente %.0fC", t_, rh_, hi_);
    switch (modo_) {
      case Modo::AGUARDANDO_CHECKIN: {
        const uint32_t faltam = prazo_ > agoraS ? (prazo_ - agoraS + 59) / 60 : 0;
        const unsigned minutos = faltam > 99 ? 99u : static_cast<unsigned>(faltam);  // o prazo é de no máximo 30 min
        escrever(t, nivel_ >= Nivel::PERIGO ? "PERIGO" : "Calor!", "Aperte o VERDE se", "esta tudo bem.", "");
        snprintf(t.linha[2], sizeof t.linha[2], "Em %02u min: familia", minutos);
        return;
      }
      case Modo::CONFIRMADO:
        escrever(t, "Obrigado!", "Beba agua e fique", "na sombra.", medida);
        return;
      case Modo::AVISANDO_FAMILIA:
        escrever(t, "Familia", recado_[0] ? recado_ : "Sua familia esta", recado_[0] ? "" : "sendo avisada.", medida);
        quebrar(t, recado_);
        return;
      case Modo::PEDIU_AJUDA:
        escrever(t, "Ajuda", "Pedido enviado.", "Sua rede de cuidado", "foi avisada.");
        return;
      case Modo::RECADO:
        escrever(t, strcmp(tipoDoRecado_, "LEMBRETE_AGUA") == 0 ? "Agua" : "Amanha", "", "", "");
        quebrar(t, recado_);
        return;
      case Modo::OCIOSO:
        break;
    }
    switch (nivel_) {
      case Nivel::NORMAL: escrever(t, "Tudo bem", medida, "", ""); break;
      case Nivel::ATENCAO: escrever(t, "Quente", "Beba agua.", medida, ""); break;
      case Nivel::CAUTELA_EXTREMA: escrever(t, "Calor!", "Fique na sombra", "e beba agua.", medida); break;
      default: escrever(t, "PERIGO", "Peca ajuda se", "nao estiver bem.", medida); break;
    }
  }

  Nivel nivel() const { return nivel_; }
  Modo modo() const { return modo_; }
  double t() const { return t_; }
  double rh() const { return rh_; }
  double hi() const { return hi_; }

  static bool noite(int horaLocal) { return horaLocal >= 22 || horaLocal < 7; }

 private:
  void pedirCheckin(uint32_t agoraS) {
    modo_ = Modo::AGUARDANDO_CHECKIN;
    desde_ = agoraS;
    prazo_ = agoraS + (nivel_ >= Nivel::PERIGO ? PRAZO_PERIGO_S : PRAZO_CAUTELA_S);
  }

  void bipar(uint32_t agoraS) {
    bipPendente_ = true;
    ultimoBip_ = agoraS;
  }

  static void copiar(char* destino, const char* origem, size_t n) {
    strncpy(destino, origem ? origem : "", n - 1);
    destino[n - 1] = '\0';
  }

  static void escrever(Tela& t, const char* titulo, const char* a, const char* b, const char* c) {
    copiar(t.titulo, titulo, sizeof t.titulo);
    copiar(t.linha[0], a, sizeof t.linha[0]);
    copiar(t.linha[1], b, sizeof t.linha[1]);
    copiar(t.linha[2], c, sizeof t.linha[2]);
  }

  // Quebra um recado do Core em até 3 linhas de 21 letras, sem cortar palavra.
  static void quebrar(Tela& t, const char* texto) {
    if (!texto || !texto[0]) return;
    memset(t.linha, 0, sizeof t.linha);
    int linha = 0;
    size_t usado = 0;
    const char* p = texto;
    while (*p && linha < 3) {
      const char* fim = p;
      while (*fim && *fim != ' ') fim++;
      const size_t palavra = static_cast<size_t>(fim - p);
      if (usado && usado + 1 + palavra > 21) {
        linha++;
        usado = 0;
        if (linha >= 3) break;
      }
      if (usado) t.linha[linha][usado++] = ' ';
      const size_t cabe = palavra > 21 - usado ? 21 - usado : palavra;
      memcpy(&t.linha[linha][usado], p, cabe);
      usado += cabe;
      p = *fim ? fim + 1 : fim;
    }
  }

  Nivel nivel_ = Nivel::NORMAL;
  Modo modo_ = Modo::OCIOSO;
  double t_ = 0, rh_ = 0, hi_ = 0;
  uint32_t desde_ = 0, prazo_ = 0, ultimoBip_ = 0;
  bool bipPendente_ = false;
  char recado_[81] = {0};
  char tipoDoRecado_[20] = {0};
};

}  // namespace sombra
