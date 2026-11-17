import * as SecureStore from 'expo-secure-store';
import { create } from 'zustand';

// O token fica no armazenamento seguro do sistema (Keychain / Keystore), nunca no SQLite.
const CHAVE_TOKEN = 'sombra.token';

// Com este "token", o cliente responde com os dados de exemplo em vez de chamar o Core.
export const TOKEN_DEMO = 'modo-demonstracao';

type Sessao = {
  token: string | null;
  carregada: boolean;
  carregar: () => Promise<void>;
  definir: (token: string) => Promise<void>;
  encerrar: () => Promise<void>;
};

export const useSessao = create<Sessao>((set) => ({
  token: null,
  carregada: false,
  carregar: async () => set({ token: await SecureStore.getItemAsync(CHAVE_TOKEN), carregada: true }),
  definir: async (token) => {
    await SecureStore.setItemAsync(CHAVE_TOKEN, token);
    set({ token });
  },
  encerrar: async () => {
    await SecureStore.deleteItemAsync(CHAVE_TOKEN);
    set({ token: null });
  },
}));
