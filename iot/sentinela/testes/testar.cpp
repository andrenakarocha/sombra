// Sombra · Sentinela · testes do firmware no host (g++): o índice de calor com os 20 vetores do contrato e a assinatura
// HMAC-SHA256 com os 6 vetores do MQTT. O mesmo código compila no ESP32.
#include <math.h>
#include <stdio.h>
#include <string.h>

#include "../contrato.h"
#include "../estado.h"
#include "../fila.h"
#include "../hmac.h"
#include "../indice_calor.h"
#include "vetores.h"

static int falhas = 0;
static void confere(bool ok, const char* descricao) {
  printf("  %s  %s\n", ok ? "ok" : "FALHOU", descricao);
  if (!ok) falhas++;
}

int main() {
  printf("índice de calor (docs/contratos/indice-de-calor.vetores.json)\n");
  for (const auto& v : VETORES_DE_CALOR) {
    const double hi = sombra::indiceDeCalorC(v.t, v.rh);
    char descricao[160];
    snprintf(descricao, sizeof descricao, "%s: %.2f °C (esperado %.2f), %s", v.caso, hi, v.hi, v.nivel);
    confere(fabs(hi - v.hi) <= TOLERANCIA_C && strcmp(sombra::nomeDoNivel(sombra::classificar(v.hi)), v.nivel) == 0,
            descricao);
  }

  printf("assinatura HMAC-SHA256 (docs/contratos/mqtt-assinatura.vetores.json)\n");
  uint8_t segredo[32];
  confere(sombra::hexParaBytes(SEGREDO_DE_TESTE, segredo, 32), "segredo de teste em hexadecimal");
  for (const auto& v : VETORES_DE_ASSINATURA) {
    char canonica[256], sig[65];
    if (v.tipo == 'T') {
      sombra::canonicaDaTelemetria(canonica, sizeof canonica, DEVICE_DE_TESTE, v.ts, v.seq, v.t, v.rh, v.hi, v.campo);
    } else if (v.tipo == 'E') {
      sombra::canonicaDoEvento(canonica, sizeof canonica, DEVICE_DE_TESTE, v.ts, v.seq, v.campo);
    } else {
      sombra::canonicaDoComando(canonica, sizeof canonica, DEVICE_DE_TESTE, v.ts, v.seq, v.campo, v.mensagem);
    }
    sombra::hmacSha256Hex(segredo, 32, canonica, sig);
    char descricao[200];
    snprintf(descricao, sizeof descricao, "%c seq %lu: %.48s", v.tipo, v.seq, v.canonica);
    confere(strcmp(canonica, v.canonica) == 0 && strcmp(sig, v.sig) == 0, descricao);
  }

  // uma mensagem adulterada depois de assinada não bate com a assinatura
  char canonica[256], sig[65];
  sombra::canonicaDaTelemetria(canonica, sizeof canonica, DEVICE_DE_TESTE, 1763400000, 42, 33.4, 40.0, 34.4, "NORMAL");
  sombra::hmacSha256Hex(segredo, 32, canonica, sig);
  confere(strcmp(sig, VETORES_DE_ASSINATURA[0].sig) != 0, "telemetria adulterada (nível trocado) tem outra assinatura");

  printf("máquina de estados (offline, D13)\n");
  {
    using sombra::Luz;
    using sombra::Modo;
    sombra::Estado e;
    sombra::Tela tela;
    e.leitura(25.0, 50.0, 0, 15);
    e.tela(tela, 0);
    confere(e.luz() == Luz::APAGADA && !e.consumirBip() && strcmp(tela.titulo, "Tudo bem") == 0,
            "normal: luz apagada, sem som, \"Tudo bem\"");

    confere(e.leitura(35.0, 50.0, 100, 15), "35 °C e 50%: o nível muda e a telemetria sai na hora");
    e.tela(tela, 100);
    confere(e.modo() == Modo::AGUARDANDO_CHECKIN && e.luz() == Luz::VERMELHA && e.consumirBip(),
            "perigo: pede o \"estou bem\", luz vermelha fixa e 3 bipes");
    confere(strcmp(tela.titulo, "PERIGO") == 0 && strstr(tela.linha[2], "Em 15 min") != nullptr,
            "a tela explica o próximo passo: em 15 min a família é avisada");

    e.tique(100 + 9 * 60, 15);
    confere(!e.consumirBip(), "o bipe não repete antes de 10 min");
    e.tique(100 + 10 * 60, 15);
    confere(e.consumirBip(), "em perigo, o bipe repete a cada 10 min");

    confere(strcmp(e.botaoVerde(800), "CHECKIN_ESTOU_BEM") == 0 && e.modo() == Modo::CONFIRMADO,
            "botão verde: evento CHECKIN_ESTOU_BEM e \"Obrigado\"");
    e.tique(811, 15);
    confere(e.modo() == Modo::OCIOSO, "depois de 10 s, a tela volta ao normal");

    sombra::Estado noite;
    noite.leitura(25.0, 50.0, 0, 23);
    noite.leitura(33.0, 50.0, 10, 23);  // cautela extrema
    confere(noite.modo() == Modo::AGUARDANDO_CHECKIN && noite.luz() == Luz::LARANJA && !noite.consumirBip(),
            "cautela à noite: pede o check-in, luz laranja e nenhum bipe (22h–7h)");
    noite.tique(10 + 30 * 60, 23);
    confere(noite.modo() == Modo::AVISANDO_FAMILIA, "sem resposta em 30 min, a tela diz que a família está sendo avisada");

    sombra::Estado core;
    core.leitura(25.0, 50.0, 0, 14);
    core.comando("PRE_ALERTA", "Amanhã: calor perigoso. Encha sua garrafa de água.", 5, 14);
    core.tela(tela, 5);
    confere(core.modo() == Modo::RECADO && core.luz() == Luz::AMARELA && strcmp(tela.titulo, "Amanha") == 0 &&
                tela.linha[0][0] != '\0',
            "comando PRE_ALERTA: o recado na tela, quebrado em linhas, com luz amarela fixa");
    core.tique(70, 14);
    confere(core.modo() == Modo::OCIOSO, "o recado some depois de 1 min");
    core.comando("FAMILIA_AVISADA", "Sua filha foi avisada.", 80, 14);
    core.tela(tela, 80);
    confere(core.modo() == Modo::AVISANDO_FAMILIA && strcmp(tela.linha[0], "Sua filha foi avisada.") != 0 &&
                strstr(tela.linha[0], "Sua filha foi") != nullptr,
            "comando FAMILIA_AVISADA: \"Sua filha foi avisada.\" no OLED");
    confere(strcmp(core.botaoVermelhoLongo(90), "PEDIDO_DE_AJUDA") == 0 && core.luz() == Luz::VERMELHA,
            "botão vermelho longo: PEDIDO_DE_AJUDA e luz vermelha");
  }

  printf("fila offline\n");
  {
    sombra::Fila<3, 32> fila;
    fila.colocar(1000, "e1");
    fila.colocar(1001, "e2");
    confere(fila.quantos() == 2 && strcmp(fila.primeiro(), "e1") == 0, "sem rede, os eventos esperam na ordem");
    fila.colocar(1002, "e3");
    fila.colocar(1003, "e4");
    confere(fila.quantos() == 3 && strcmp(fila.primeiro(), "e2") == 0 && fila.descartados() == 1,
            "cheia, a fila descarta o mais antigo e conta o descarte");
    confere(fila.descartarExpirados(1001 + 270, 270) == 0 && fila.quantos() == 3,
            "a rede volta em 4,5 min: nada expirou, tudo vai para o Core");
    confere(fila.descartarExpirados(1002 + 271, 270) == 2 && strcmp(fila.primeiro(), "e4") == 0 && fila.expirados() == 2,
            "o que passou da janela de 5 min sai sem ser enviado (o Core recusaria como replay)");
    fila.retirar();
    confere(fila.quantos() == 0 && fila.primeiro() == nullptr, "a rede voltou: tudo enviado, fila vazia");
  }

  if (falhas) {
    printf("%d falha(s)\n", falhas);
  } else {
    printf("tudo certo\n");
  }
  return falhas ? 1 : 0;
}
