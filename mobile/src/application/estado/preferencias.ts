import { createStore } from 'zustand/vanilla';

export type EstadoDasPreferencias = {
  // Com uma casa escolhida, este celular abre direto no Modo Idoso daquela casa.
  modoIdosoCasaId: string | null;
  carregada: boolean;
};

export const preferenciasStore = createStore<EstadoDasPreferencias>(() => ({ modoIdosoCasaId: null, carregada: false }));
