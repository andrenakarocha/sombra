import { formatarGraus, formatarNumero, haQuanto } from '@/dominio/formato';

describe('formatos pt-BR', () => {
  it('números e graus com vírgula', () => {
    expect(formatarNumero(6.5, 1)).toBe('6,5');
    expect(formatarGraus(35.47)).toBe('35,5 °C');
    expect(formatarNumero(null)).toBe('—');
  });

  it('tempo relativo', () => {
    const agora = Date.parse('2026-11-18T18:00:00Z');
    expect(haQuanto('2026-11-18T17:55:00Z', agora)).toBe('há 5 minutos');
    expect(haQuanto('2026-11-18T20:00:00Z', agora)).toBe('em 2 horas');
    expect(haQuanto(null, agora)).toBe('—');
  });
});
