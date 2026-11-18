import { createStore } from 'zustand/vanilla';

import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

// O replay de dados reais em andamento (modo demonstração), para a tela mostrar de onde vem cada hora.
export type EstadoDaReproducao = {
  ativo: boolean;
  passo: number;
  total: number;
  momentoOriginal: string | null;
  temperaturaC: number | null;
  indiceC: number | null;
  nivelPadrao: NivelDeRisco | null;
  nivelPersonalizado: NivelDeRisco | null;
  fonte: string | null;
};

export const PARADA: EstadoDaReproducao = {
  ativo: false,
  passo: 0,
  total: 0,
  momentoOriginal: null,
  temperaturaC: null,
  indiceC: null,
  nivelPadrao: null,
  nivelPersonalizado: null,
  fonte: null,
};

export const reproducaoStore = createStore<EstadoDaReproducao>(() => PARADA);
