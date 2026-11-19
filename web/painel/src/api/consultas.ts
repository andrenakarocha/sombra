import { useQuery } from '@tanstack/react-query';
import { CHAVES } from '@/app/consultas';
import { pedir } from './http';
import { AlertaSchema, DomicilioSchema, pagina, ResumoSchema } from './schemas';

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
