import { createStore } from 'zustand/vanilla';

// Com este "token", o app usa o gateway da demonstração em vez de chamar o Core.
export const TOKEN_DEMO = 'modo-demonstracao';

export type EstadoDaSessao = { token: string | null; carregada: boolean };

// Store sem React: a infraestrutura (cliente HTTP, hub) lê daqui; as telas assinam pelo hook da apresentação.
export const sessaoStore = createStore<EstadoDaSessao>(() => ({ token: null, carregada: false }));

export const emDemonstracao = () => sessaoStore.getState().token === TOKEN_DEMO;
