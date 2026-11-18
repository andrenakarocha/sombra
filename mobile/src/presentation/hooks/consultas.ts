// Hooks do TanStack Query sobre os casos de uso. As chaves batem com os recursos do barramento.

import { useMutation, useQuery } from '@tanstack/react-query';

import type { AcaoDeReconhecimento, NovoMorador, SituacaoDoCheckIn } from '@/application/modelos';

import { useContainer } from '../providers/container';

export const chaves = {
  casas: ['casas'] as const,
  casa: (id: string) => ['casa', id] as const,
  alertas: ['alertas'] as const,
  alerta: (id: string) => ['alerta', id] as const,
  pontos: (id: string) => ['pontos', id] as const,
  privacidade: (id: string) => ['privacidade', id] as const,
  sincronizacao: ['sincronizacao'] as const,
};

export function useCasas() {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.casas, queryFn: () => consultas.casas() });
}

export function usePainelDaCasa(id: string | undefined) {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.casa(id ?? ''), enabled: !!id, queryFn: () => consultas.painelDaCasa(id!) });
}

export function useAlertas() {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.alertas, queryFn: () => consultas.alertas() });
}

export function useAlerta(id: string) {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.alerta(id), queryFn: () => consultas.alerta(id) });
}

export function usePontosDeResfriamento(id: string | undefined) {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.pontos(id ?? ''), enabled: !!id, queryFn: () => consultas.pontosDeResfriamento(id!) });
}

export function usePrivacidade(id: string | undefined) {
  const { consultas } = useContainer();
  return useQuery({ queryKey: chaves.privacidade(id ?? ''), enabled: !!id, queryFn: () => consultas.privacidade(id!) });
}

export function useReconhecer(alertaId: string) {
  const { escritas } = useContainer();
  return useMutation({ mutationFn: (acao: AcaoDeReconhecimento) => escritas.reconhecerAlerta(alertaId, acao) });
}

export function useCheckIn(domicilioId: string) {
  const { escritas } = useContainer();
  return useMutation({ mutationFn: (situacao: SituacaoDoCheckIn) => escritas.registrarCheckIn(domicilioId, situacao) });
}

export function useSalvarMorador(domicilioId: string, moradorId?: string) {
  const { escritas } = useContainer();
  return useMutation({ mutationFn: (dados: NovoMorador) => escritas.salvarMorador(domicilioId, moradorId, dados) });
}

export function useSincronizacao() {
  const { central } = useContainer();
  return useQuery({ queryKey: chaves.sincronizacao, queryFn: () => central.resumo() });
}
