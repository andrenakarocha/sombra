// Os cinco níveis de calor do contrato docs/contratos/niveis-de-risco.md.

export const NIVEIS = ['NORMAL', 'ATENCAO', 'CAUTELA_EXTREMA', 'PERIGO', 'PERIGO_EXTREMO'] as const;

export type NivelDeRisco = (typeof NIVEIS)[number];

type InfoDoNivel = {
  rotulo: string;
  frase: string;
  token: 'normal' | 'atencao' | 'cautelaExtrema' | 'perigo' | 'perigoExtremo';
};

export const INFO_DO_NIVEL: Record<NivelDeRisco, InfoDoNivel> = {
  NORMAL: { rotulo: 'Normal', frase: 'Tudo bem por aqui.', token: 'normal' },
  ATENCAO: { rotulo: 'Atenção', frase: 'Está quente. Beba água.', token: 'atencao' },
  CAUTELA_EXTREMA: {
    rotulo: 'Cautela extrema',
    frase: 'Calor forte. Fique na sombra e beba água.',
    token: 'cautelaExtrema',
  },
  PERIGO: {
    rotulo: 'Perigo',
    frase: 'Calor perigoso. Aperte o botão verde se você está bem.',
    token: 'perigo',
  },
  PERIGO_EXTREMO: { rotulo: 'Perigo extremo', frase: 'Calor extremo. Peça ajuda.', token: 'perigoExtremo' },
};
