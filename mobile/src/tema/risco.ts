import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

// Qual conjunto de cores do tema cada nível usa (a regra do nível fica no domínio; a cor, aqui).
export const TOKEN_DO_NIVEL = {
  NORMAL: 'normal',
  ATENCAO: 'atencao',
  CAUTELA_EXTREMA: 'cautelaExtrema',
  PERIGO: 'perigo',
  PERIGO_EXTREMO: 'perigoExtremo',
} as const satisfies Record<NivelDeRisco, string>;
