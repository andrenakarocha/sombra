import { createStore } from 'zustand/vanilla';

export type EstadoDoBloqueio = { ativo: boolean; bloqueado: boolean };

export const bloqueioStore = createStore<EstadoDoBloqueio>(() => ({ ativo: false, bloqueado: false }));
