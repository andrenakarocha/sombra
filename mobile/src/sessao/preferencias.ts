import { create } from 'zustand';

import { gravarPreferencia, lerPreferencia } from '@/banco/preferencias';

const MODO_IDOSO = 'modoIdosoCasaId';

type Preferencias = {
  // Com uma casa escolhida, este celular abre direto no Modo Idoso daquela casa.
  modoIdosoCasaId: string | null;
  carregada: boolean;
  carregar: () => Promise<void>;
  definirModoIdoso: (casaId: string | null) => Promise<void>;
};

export const usePreferencias = create<Preferencias>((set) => ({
  modoIdosoCasaId: null,
  carregada: false,
  carregar: async () => set({ modoIdosoCasaId: await lerPreferencia(MODO_IDOSO), carregada: true }),
  definirModoIdoso: async (casaId) => {
    await gravarPreferencia(MODO_IDOSO, casaId);
    set({ modoIdosoCasaId: casaId });
  },
}));
