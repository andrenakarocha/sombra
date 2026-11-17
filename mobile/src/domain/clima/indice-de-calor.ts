// Índice de calor pela equação da NWS/WPC (Rothfusz 1990, SR 90-23), na leitura adotada pelo Sombra
// (docs/contratos/niveis-de-risco.md). Implementação de referência: tools/indice-de-calor/vetores.py.

import { classificar, type NivelDeRisco } from './nivel-de-risco';
import type { Temperatura } from './temperatura';
import type { UmidadeRelativa } from './umidade-relativa';

function emFahrenheit(t: number, ur: number): number {
  const simples = 0.5 * (t + 61 + (t - 68) * 1.2 + ur * 0.094);
  if ((simples + t) / 2 < 80) return simples;
  let hi =
    -42.379 +
    2.04901523 * t +
    10.14333127 * ur -
    0.22475541 * t * ur -
    0.00683783 * t * t -
    0.05481717 * ur * ur +
    0.00122874 * t * t * ur +
    0.00085282 * t * ur * ur -
    0.00000199 * t * t * ur * ur;
  if (ur < 13 && t >= 80 && t <= 112) hi -= ((13 - ur) / 4) * Math.sqrt((17 - Math.abs(t - 95)) / 17);
  else if (ur > 85 && t >= 80 && t <= 87) hi += ((ur - 85) / 10) * ((87 - t) / 5);
  return hi;
}

export class IndiceDeCalor {
  private constructor(readonly celsius: number) {}

  static calcular(temperatura: Temperatura, umidade: UmidadeRelativa): IndiceDeCalor {
    const f = emFahrenheit(temperatura.fahrenheit, umidade.percentual);
    return new IndiceDeCalor(((f - 32) * 5) / 9);
  }

  // Valor que já veio calculado (Core, Sentinela): só reconstitui.
  static restaurar(celsius: number) {
    return new IndiceDeCalor(celsius);
  }

  get nivel(): NivelDeRisco {
    return classificar(this.celsius);
  }
}
