import { falha, ok, type Result } from '../compartilhado/result';

// Faixa física plausível para um sensor doméstico; fora dela é leitura errada, não calor.
const MINIMA_C = -40;
const MAXIMA_C = 60;

export class Temperatura {
  private constructor(readonly celsius: number) {}

  static emCelsius(valor: number): Result<Temperatura> {
    if (!Number.isFinite(valor)) return falha('TEMPERATURA_INVALIDA', 'A temperatura não é um número.');
    if (valor < MINIMA_C || valor > MAXIMA_C) {
      return falha('TEMPERATURA_FORA_DA_FAIXA', `Temperatura fora da faixa de ${MINIMA_C} a ${MAXIMA_C} °C.`);
    }
    return ok(new Temperatura(valor));
  }

  get fahrenheit() {
    return (this.celsius * 9) / 5 + 32;
  }
}
