// Sombra · Sentinela · índice de calor (NWS/WPC, Rothfusz 1990 com os dois ajustes e a fórmula simples de Steadman),
// a mesma leitura de docs/contratos/niveis-de-risco.md que o Core, o Clima e o ML usam. Os 20 vetores do contrato rodam
// em testes/testar.cpp. Calcular aqui deixa a Sentinela alertar mesmo sem internet.
#pragma once
#include <math.h>

namespace sombra {

enum class Nivel { NORMAL = 0, ATENCAO, CAUTELA_EXTREMA, PERIGO, PERIGO_EXTREMO };

inline double indiceDeCalorC(double temperaturaC, double umidadePct) {
  const double t = temperaturaC * 9.0 / 5.0 + 32.0;
  const double ur = umidadePct;
  const double simples = 0.5 * (t + 61.0 + (t - 68.0) * 1.2 + ur * 0.094);
  double f = simples;
  if ((simples + t) / 2.0 >= 80.0) {
    f = -42.379 + 2.04901523 * t + 10.14333127 * ur - 0.22475541 * t * ur - 0.00683783 * t * t -
        0.05481717 * ur * ur + 0.00122874 * t * t * ur + 0.00085282 * t * ur * ur - 0.00000199 * t * t * ur * ur;
    if (ur < 13.0 && t >= 80.0 && t <= 112.0) {
      f -= ((13.0 - ur) / 4.0) * sqrt((17.0 - fabs(t - 95.0)) / 17.0);
    } else if (ur > 85.0 && t >= 80.0 && t <= 87.0) {
      f += ((ur - 85.0) / 10.0) * ((87.0 - t) / 5.0);
    }
  }
  return (f - 32.0) * 5.0 / 9.0;
}

// Os limiares da tabela da NWS em °C (intervalo fechado embaixo e aberto em cima).
inline Nivel classificar(double indiceC) {
  if (indiceC >= 51.67) return Nivel::PERIGO_EXTREMO;
  if (indiceC >= 39.44) return Nivel::PERIGO;
  if (indiceC >= 32.22) return Nivel::CAUTELA_EXTREMA;
  if (indiceC >= 26.67) return Nivel::ATENCAO;
  return Nivel::NORMAL;
}

inline const char* nomeDoNivel(Nivel n) {
  switch (n) {
    case Nivel::NORMAL: return "NORMAL";
    case Nivel::ATENCAO: return "ATENCAO";
    case Nivel::CAUTELA_EXTREMA: return "CAUTELA_EXTREMA";
    case Nivel::PERIGO: return "PERIGO";
    case Nivel::PERIGO_EXTREMO: return "PERIGO_EXTREMO";
  }
  return "NORMAL";
}

// Uma casa decimal, como no contrato: a Sentinela arredonda t, rh e hi antes de montar o payload e a assinatura.
inline double umaCasa(double v) { return round(v * 10.0) / 10.0; }

}  // namespace sombra
