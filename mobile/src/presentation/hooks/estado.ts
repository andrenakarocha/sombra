import { useStore } from 'zustand';

import { type EstadoDasPreferencias, preferenciasStore } from '@/application/estado/preferencias';
import { type EstadoDaSessao, sessaoStore, TOKEN_DEMO } from '@/application/estado/sessao';

export const useSessao = <T,>(seletor: (s: EstadoDaSessao) => T) => useStore(sessaoStore, seletor);

export const usePreferencias = <T,>(seletor: (s: EstadoDasPreferencias) => T) => useStore(preferenciasStore, seletor);

export const useEmDemonstracao = () => useSessao((s) => s.token === TOKEN_DEMO);
