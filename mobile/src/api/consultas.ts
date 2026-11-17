import { onlineManager, QueryClient, useQuery } from '@tanstack/react-query';
import * as Network from 'expo-network';

import { type Cacheado, lerCasas, salvarCasas } from '@/banco/casas';

import { ErroDaApi, requisitar, SemConexao } from './cliente';
import type { Domicilio, Pagina } from './tipos';

// networkMode 'always': offline, a consulta roda mesmo assim e cai no cache do SQLite em vez de ficar pausada.
export const queryClient = new QueryClient({
  defaultOptions: { queries: { networkMode: 'always', retry: 1, staleTime: 30_000 } },
});

// Quando a rede volta, o TanStack Query refaz as consultas.
onlineManager.setEventListener((definirOnline) => {
  const assinatura = Network.addNetworkStateListener((estado) => definirOnline(estado.isConnected ?? true));
  return () => assinatura.remove();
});

export type Resultado<T> = Cacheado<T> & { origem: 'api' | 'cache' };

function apiForaDoAr(erro: unknown) {
  return erro instanceof SemConexao || (erro instanceof ErroDaApi && erro.status >= 500);
}

export async function comCache<T>(
  buscar: () => Promise<T>,
  salvar: (dados: T) => Promise<void>,
  ler: () => Promise<Cacheado<T> | null>,
): Promise<Resultado<T>> {
  try {
    const dados = await buscar();
    await salvar(dados);
    return { dados, atualizadoEm: new Date().toISOString(), origem: 'api' };
  } catch (erro) {
    if (!apiForaDoAr(erro)) throw erro;
    const cache = await ler();
    if (!cache) throw erro;
    return { ...cache, origem: 'cache' };
  }
}

export function useCasas() {
  return useQuery({
    queryKey: ['casas'],
    queryFn: () =>
      comCache(
        async () => (await requisitar<Pagina<Domicilio>>('/domicilios?tamanho=100')).itens,
        salvarCasas,
        lerCasas,
      ),
  });
}
