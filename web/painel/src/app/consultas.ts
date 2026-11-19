import { QueryClient } from '@tanstack/react-query';

/** O cache do painel. Os eventos do SignalR invalidam as chaves certas: a tela muda sem polling. */
export const consultas = new QueryClient({
  defaultOptions: {
    queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false },
  },
});

export const CHAVES = {
  resumo: ['resumo'] as const,
  alertas: ['alertas'] as const,
  casas: ['casas'] as const,
  risco: ['risco-cidadao'] as const,
  saude: ['saude'] as const,
};
