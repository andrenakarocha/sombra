import vetores from '../../../docs/contratos/indice-de-calor.vetores.json';

import { IndiceDeCalor } from '@/domain/clima/indice-de-calor';
import { classificar, LIMIARES_C, NIVEIS } from '@/domain/clima/nivel-de-risco';
import { Temperatura } from '@/domain/clima/temperatura';
import { UmidadeRelativa } from '@/domain/clima/umidade-relativa';
import { valorOuErro } from '@/domain/compartilhado/result';

const indice = (t: number, ur: number) =>
  IndiceDeCalor.calcular(valorOuErro(Temperatura.emCelsius(t)), valorOuErro(UmidadeRelativa.de(ur)));

describe('índice de calor: os vetores compartilhados do contrato (C#, Java, C++, Python e agora TypeScript)', () => {
  it('usa os mesmos limiares do contrato', () => {
    expect(LIMIARES_C).toEqual(vetores.limiaresC);
  });

  it.each(vetores.vetores.map((v) => [v.caso, v] as const))('%s', (_, v) => {
    const hi = indice(v.temperaturaC, v.umidadePct);
    expect(Math.abs(hi.celsius - v.indiceDeCalorC)).toBeLessThanOrEqual(vetores.toleranciaC);
    expect(hi.nivel).toBe(v.nivel);
  });

  it('os vetores cobrem os cinco níveis', () => {
    expect(new Set(vetores.vetores.map((v) => v.nivel))).toEqual(new Set(NIVEIS));
  });
});

describe('classificar', () => {
  it('intervalo fechado embaixo e aberto em cima', () => {
    expect(classificar(26.66)).toBe('NORMAL');
    expect(classificar(26.67)).toBe('ATENCAO');
    expect(classificar(39.44)).toBe('PERIGO');
    expect(classificar(51.67)).toBe('PERIGO_EXTREMO');
  });
});

describe('value objects do clima recusam leitura impossível', () => {
  it.each([
    [Number.NaN, 'TEMPERATURA_INVALIDA'],
    [-41, 'TEMPERATURA_FORA_DA_FAIXA'],
    [61, 'TEMPERATURA_FORA_DA_FAIXA'],
  ])('temperatura %p', (valor, codigo) => {
    const r = Temperatura.emCelsius(valor);
    expect(r.ok).toBe(false);
    if (!r.ok) expect(r.erro.codigo).toBe(codigo);
  });

  it.each([-1, 101, Number.POSITIVE_INFINITY])('umidade %p', (valor) => {
    expect(UmidadeRelativa.de(valor).ok).toBe(false);
  });

  it('°C para °F', () => {
    expect(valorOuErro(Temperatura.emCelsius(37)).fahrenheit).toBeCloseTo(98.6);
  });

  it('valorOuErro explode com o código quando o valor é inválido', () => {
    expect(() => valorOuErro(Temperatura.emCelsius(99))).toThrow('TEMPERATURA_FORA_DA_FAIXA');
  });

  it('restaurar reconstitui um índice já calculado pelo Core', () => {
    expect(IndiceDeCalor.restaurar(42.1).nivel).toBe('PERIGO');
  });
});
