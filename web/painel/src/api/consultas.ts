import { useQuery } from '@tanstack/react-query';
import { CHAVES } from '@/app/consultas';
import { pedir } from './http';
import { AlertaSchema, DomicilioSchema, EventoDeSegurancaSchema, pagina, ResumoSchema, RiscoCidadaoSchema } from './schemas';

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

export function useEventosDeSeguranca() {
  return useQuery({
    queryKey: [...CHAVES.risco, 'eventos'],
    queryFn: () => pedir('/api/v1/privacidade/eventos?pagina=1&tamanho=100', pagina(EventoDeSegurancaSchema)),
  });
}
