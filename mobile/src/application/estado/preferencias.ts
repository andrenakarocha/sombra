import { createStore } from 'zustand/vanilla';

// Tamanho do texto escolhido no app, por cima da fonte do sistema (que continua valendo).
export const ESCALAS_DO_TEXTO = { NORMAL: 1, GRANDE: 1.2, MAIOR: 1.4 } as const;
export type EscalaDoTexto = keyof typeof ESCALAS_DO_TEXTO;

export const ehEscalaDoTexto = (valor: string | null): valor is EscalaDoTexto =>
  valor !== null && Object.hasOwn(ESCALAS_DO_TEXTO, valor);

export type EstadoDasPreferencias = {
  // Com uma casa escolhida, este celular abre direto no Modo Idoso daquela casa.
  modoIdosoCasaId: string | null;
  escalaDoTexto: EscalaDoTexto;
  carregada: boolean;
};

export const preferenciasStore = createStore<EstadoDasPreferencias>(() => ({
  modoIdosoCasaId: null,
  escalaDoTexto: 'NORMAL',
  carregada: false,
}));
