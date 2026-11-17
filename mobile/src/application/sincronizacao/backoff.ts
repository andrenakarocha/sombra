// Backoff exponencial com jitter ("equal jitter"): metade do atraso é fixa, a outra metade é sorteada, para mil
// celulares que voltaram da mesma queda de rede não baterem no Core no mesmo segundo.

export const BASE_MS = 2_000;
export const TETO_MS = 5 * 60_000;
export const MAXIMO_DE_TENTATIVAS = 6;

export function atrasoDaTentativa(tentativa: number, sorteio: () => number = Math.random): number {
  const exponencial = Math.min(TETO_MS, BASE_MS * 2 ** (tentativa - 1));
  return Math.round(exponencial / 2 + sorteio() * (exponencial / 2));
}
