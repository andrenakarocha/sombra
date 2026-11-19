import { useQuery } from '@tanstack/react-query';
import { CHAVES } from '@/app/consultas';
import { pedir } from './http';
import {
  AlertaSchema,
  DomicilioSchema,
  EventoDeSegurancaSchema,
  InfoDoClimaSchema,
  pagina,
  ProntidaoSchema,
  ResumoSchema,
  RiscoCidadaoSchema,
  SaudeDoClimaSchema,
  VersaoSchema,
} from './schemas';

export function useResumo() {
  return useQuery({ queryKey: CHAVES.resumo, queryFn: () => pedir('/api/v1/painel/resumo', ResumoSchema) });
}

export interface FiltroDeAlertas {
  estado?: string;
  nivel?: string;
  pagina?: number;
}

export function useAlertas(filtro: FiltroDeAlertas = {}) {
  const params = new URLSearchParams({ tamanho: '20', pagina: String(filtro.pagina ?? 1) });
  if (filtro.estado) params.set('estado', filtro.estado);
  if (filtro.nivel) params.set('nivel', filtro.nivel);
  return useQuery({
    queryKey: [...CHAVES.alertas, filtro],
    queryFn: () => pedir(`/api/v1/alertas?${params}`, pagina(AlertaSchema)),
  });
}

export function useCasas(filtro: { nivel?: string; bairro?: string } = {}) {
  const params = new URLSearchParams({ tamanho: '100' });
  if (filtro.nivel) params.set('nivel', filtro.nivel);
  if (filtro.bairro) params.set('bairro', filtro.bairro);
  return useQuery({
    queryKey: [...CHAVES.casas, filtro],
    queryFn: () => pedir(`/api/v1/domicilios?${params}`, pagina(DomicilioSchema)),
  });
}

/** O farol global e por categoria; o evento RiscoCidadaoAtualizado do hub invalida esta chave e a dos eventos. */
export function useRiscoCidadao() {
  return useQuery({ queryKey: CHAVES.risco, queryFn: () => pedir('/api/v1/privacidade/risco-cidadao', RiscoCidadaoSchema) });
}

/**
 * Os eventos com a tradução. O hub avisa quando chega evento novo, mas a tradução sai depois, no worker do Core: enquanto
 * algum evento estiver na fila da tradução, a lista se atualiza sozinha a cada 4 s.
 */
export function useEventosDeSeguranca() {
  return useQuery({
    queryKey: [...CHAVES.risco, 'eventos'],
    queryFn: () => pedir('/api/v1/privacidade/eventos?pagina=1&tamanho=100', pagina(EventoDeSegurancaSchema)),
    refetchInterval: (q) => (q.state.data?.itens.some((e) => !e.traducao) ? 4_000 : false),
  });
}

const SAUDE = { refetchInterval: 15_000, retry: 0 } as const;

/** A saúde de cada serviço, de 15 em 15 s (é o que o monitor da VM também faz). Falha vira "fora do ar", não erro. */
export function useSaudeDoCore() {
  return useQuery({
    queryKey: [...CHAVES.saude, 'core'],
    ...SAUDE,
    queryFn: async () => ({
      pronto: await pedir('/health/ready', ProntidaoSchema).catch(() => null),
      versao: await pedir('/version', VersaoSchema).catch(() => null),
    }),
  });
}

export function useSaudeDoClima() {
  return useQuery({
    queryKey: [...CHAVES.saude, 'clima'],
    ...SAUDE,
    queryFn: async () => ({
      pronto: await pedir('/clima/actuator/health/readiness', SaudeDoClimaSchema, {}, true).catch(() => null),
      info: await pedir('/clima/actuator/info', InfoDoClimaSchema, {}, true).catch(() => null),
    }),
  });
}
