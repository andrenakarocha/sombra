export const FATORES_DE_RISCO = ['IDADE_AVANCADA', 'CARDIOPATIA', 'USO_DE_DIURETICO'] as const;

export type FatorDeRisco = (typeof FATORES_DE_RISCO)[number];

// R3: ≥ 80 anos conta mesmo que ninguém marque a caixa.
export function fatoresEfetivos(fatores: readonly FatorDeRisco[], idade: number): FatorDeRisco[] {
  const todos = new Set(fatores);
  if (idade >= 80) todos.add('IDADE_AVANCADA');
  return FATORES_DE_RISCO.filter((f) => todos.has(f));
}
