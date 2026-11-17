import { onlineManager, QueryClient, useMutation, useQuery } from '@tanstack/react-query';
import * as Network from 'expo-network';

import { lerAlertas, salvarAlertas } from '@/banco/alertas';
import { type Cacheado, lerCasa, lerCasas, salvarCasa, salvarCasas } from '@/banco/casas';

import { ErroDaApi, requisitar, SemConexao } from './cliente';
import type {
  AcaoDeReconhecimento,
  Alerta,
  ContatoDaRede,
  Domicilio,
  Leitura,
  Morador,
  NovoMorador,
  Pagina,
  PontoDeResfriamento,
  PrivacidadeDaCasa,
} from './tipos';

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
    // O dado já chegou: se o cache falhar, a tela mostra mesmo assim.
    await salvar(dados).catch(() => {});
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

export function useCasa(id: string) {
  return useQuery({
    queryKey: ['casa', id],
    queryFn: () =>
      comCache(
        () => requisitar<Domicilio>(`/domicilios/${id}`),
        salvarCasa,
        () => lerCasa(id),
      ),
  });
}

// Leituras das últimas 24 h, da mais antiga para a mais recente (a API devolve da mais recente para a mais antiga).
export function useLeituras24h(id: string) {
  return useQuery({
    queryKey: ['leituras', id],
    queryFn: async () => {
      const ate = new Date();
      const de = new Date(ate.getTime() - 24 * 60 * 60 * 1000);
      const pagina = await requisitar<Pagina<Leitura>>(
        `/domicilios/${id}/leituras?de=${de.toISOString()}&ate=${ate.toISOString()}&tamanho=100`,
      );
      return [...pagina.itens].reverse();
    },
  });
}

export function useMoradores(id: string) {
  return useQuery({ queryKey: ['moradores', id], queryFn: () => requisitar<Morador[]>(`/domicilios/${id}/moradores`) });
}

export function useRedeDeCuidado(id: string) {
  return useQuery({
    queryKey: ['rede', id],
    queryFn: () => requisitar<ContatoDaRede[]>(`/domicilios/${id}/rede-de-cuidado`),
  });
}

export function useAlertas() {
  return useQuery({
    queryKey: ['alertas'],
    queryFn: () =>
      comCache(
        async () => (await requisitar<Pagina<Alerta>>('/alertas?tamanho=100')).itens,
        salvarAlertas,
        lerAlertas,
      ),
  });
}

export function useAlerta(id: string) {
  return useQuery({ queryKey: ['alerta', id], queryFn: () => requisitar<Alerta>(`/alertas/${id}`) });
}

export function useReconhecer(id: string) {
  return useMutation({
    mutationFn: (acao: AcaoDeReconhecimento) =>
      requisitar<void>(`/alertas/${id}/reconhecer`, { metodo: 'POST', corpo: { acao } }),
    onSuccess: () => queryClient.invalidateQueries(),
  });
}

export function usePontosDeResfriamento(id: string | undefined) {
  return useQuery({
    queryKey: ['pontos', id],
    enabled: !!id,
    queryFn: () => requisitar<PontoDeResfriamento[]>(`/domicilios/${id}/pontos-de-resfriamento?raioKm=3`),
  });
}

export function usePrivacidade(id: string | undefined) {
  return useQuery({
    queryKey: ['privacidade', id],
    enabled: !!id,
    queryFn: () => requisitar<PrivacidadeDaCasa>(`/domicilios/${id}/privacidade`),
  });
}

export function useSalvarMorador(domicilioId: string, moradorId?: string) {
  return useMutation({
    mutationFn: (morador: NovoMorador) =>
      requisitar<Morador>(`/domicilios/${domicilioId}/moradores${moradorId ? `/${moradorId}` : ''}`, {
        metodo: moradorId ? 'PUT' : 'POST',
        corpo: morador,
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['moradores', domicilioId] }),
  });
}
