import { useColorScheme } from 'react-native';

import { tokens } from './tokens';

export type Tema = typeof tokens.tema.claro | typeof tokens.tema.escuro;

// Famílias carregadas no _layout a partir de ./fontes.
export const fonte = {
  texto: 'AtkinsonHyperlegibleNext_400Regular',
  textoForte: 'AtkinsonHyperlegibleNext_700Bold',
  textoExtra: 'AtkinsonHyperlegibleNext_800ExtraBold',
  mono: 'AtkinsonHyperlegibleMono_400Regular',
  display: 'Fraunces_700Bold',
} as const;

export const { espaco, raio, tamanho, toque, duracao } = tokens;

export function useTema(): Tema {
  return useColorScheme() === 'dark' ? tokens.tema.escuro : tokens.tema.claro;
}
