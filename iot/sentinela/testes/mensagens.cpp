// Sombra · Sentinela · as mensagens MQTT exatamente como o firmware (os mesmos contrato.h, hmac.h e indice_calor.h), para a
// prova de integração com o Core de verdade (testes/integrar-com-core.sh): monta e assina o que a Sentinela envia, e
// confere o comando que o Core manda de volta, como o aoReceber do sketch. O segredo vem do ambiente (SEGREDO_HEX) e nunca
// é impresso.
//   mensagens telemetria <deviceId> <ts> <seq> <t> <rh> [--adulterar]
//   mensagens evento     <deviceId> <ts> <seq> <tipo>
//   mensagens conferir   <deviceId> <ts> <seq> <tipo> <mensagem> <sig>    (sai com 0 se o comando vale)
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

#include "../contrato.h"
#include "../hmac.h"
#include "../indice_calor.h"

int main(int argc, char** argv) {
  const char* hex = getenv("SEGREDO_HEX");
  if (argc < 6 || !hex || strlen(hex) != 64) {
    fprintf(stderr, "uso: SEGREDO_HEX=… mensagens telemetria|evento|conferir <deviceId> <ts> <seq> …\n");
    return 2;
  }
  uint8_t segredo[32];
  sombra::hexParaBytes(hex, segredo, 32);
  const char* canal = argv[1];
  const char* device = argv[2];
  const uint32_t ts = static_cast<uint32_t>(strtoul(argv[3], nullptr, 10));
  const uint32_t seq = static_cast<uint32_t>(strtoul(argv[4], nullptr, 10));
  char canonica[200], sig[65];

  if (strcmp(canal, "conferir") == 0) {
    if (argc < 8) return 2;
    sombra::canonicaDoComando(canonica, sizeof canonica, device, ts, seq, argv[5], argv[6]);
    sombra::hmacSha256Hex(segredo, 32, canonica, sig);
    const double desvio = fabs(difftime(time(nullptr), static_cast<time_t>(ts)));
    const bool assinaturaOk = strcmp(sig, argv[7]) == 0, horaOk = desvio <= 300;
    printf("assinatura %s · ts a %.0f s do relógio (%s)\n", assinaturaOk ? "confere" : "NÃO confere", desvio,
           horaOk ? "dentro da janela" : "FORA da janela");
    return assinaturaOk && horaOk ? 0 : 1;
  }

  if (strcmp(canal, "evento") == 0) {
    sombra::canonicaDoEvento(canonica, sizeof canonica, device, ts, seq, argv[5]);
    sombra::hmacSha256Hex(segredo, 32, canonica, sig);
    printf("{\"v\":1,\"ts\":%u,\"seq\":%u,\"tipo\":\"%s\",\"sig\":\"%s\"}\n", ts, seq, argv[5], sig);
    return 0;
  }

  if (argc < 7) return 2;
  const double t = sombra::umaCasa(atof(argv[5])), rh = sombra::umaCasa(atof(argv[6]));
  const double hi = sombra::umaCasa(sombra::indiceDeCalorC(t, rh));
  const char* nivel = sombra::nomeDoNivel(sombra::classificar(hi));
  sombra::canonicaDaTelemetria(canonica, sizeof canonica, device, ts, seq, t, rh, hi, nivel);
  sombra::hmacSha256Hex(segredo, 32, canonica, sig);
  // adulterada: alguém no broker público troca o nível depois da assinatura, para esconder o perigo
  const bool adulterar = argc > 7 && strcmp(argv[7], "--adulterar") == 0;
  printf("{\"v\":1,\"ts\":%u,\"seq\":%u,\"t\":%.1f,\"rh\":%.1f,\"hi\":%.1f,\"nivel\":\"%s\",\"sig\":\"%s\"}\n", ts, seq, t, rh,
         hi, adulterar ? "NORMAL" : nivel, sig);
  return 0;
}
