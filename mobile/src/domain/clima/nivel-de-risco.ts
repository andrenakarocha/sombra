// Os cinco níveis de calor do contrato docs/contratos/niveis-de-risco.md (faixas da NWS em °C, fechado embaixo).

export const NIVEIS = ['NORMAL', 'ATENCAO', 'CAUTELA_EXTREMA', 'PERIGO', 'PERIGO_EXTREMO'] as const;

export type NivelDeRisco = (typeof NIVEIS)[number];

export const LIMIARES_C = { ATENCAO: 26.67, CAUTELA_EXTREMA: 32.22, PERIGO: 39.44, PERIGO_EXTREMO: 51.67 } as const;

export function classificar(indiceC: number, limiares: Record<keyof typeof LIMIARES_C, number> = LIMIARES_C): NivelDeRisco {
  if (indiceC >= limiares.PERIGO_EXTREMO) return 'PERIGO_EXTREMO';
  if (indiceC >= limiares.PERIGO) return 'PERIGO';
  if (indiceC >= limiares.CAUTELA_EXTREMA) return 'CAUTELA_EXTREMA';
  if (indiceC >= limiares.ATENCAO) return 'ATENCAO';
  return 'NORMAL';
}

export const gravidade = (nivel: NivelDeRisco) => NIVEIS.indexOf(nivel);

// Texto para o cidadão, igual ao do contrato.
export const INFO_DO_NIVEL: Record<NivelDeRisco, { rotulo: string; frase: string }> = {
  NORMAL: { rotulo: 'Normal', frase: 'Tudo bem por aqui.' },
  ATENCAO: { rotulo: 'Atenção', frase: 'Está quente. Beba água.' },
  CAUTELA_EXTREMA: { rotulo: 'Cautela extrema', frase: 'Calor forte. Fique na sombra e beba água.' },
  PERIGO: { rotulo: 'Perigo', frase: 'Calor perigoso. Aperte o botão verde se você está bem.' },
  PERIGO_EXTREMO: { rotulo: 'Perigo extremo', frase: 'Calor extremo. Peça ajuda.' },
};
