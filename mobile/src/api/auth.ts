import { limparCache } from '@/banco/casas';
import { usePreferencias } from '@/sessao/preferencias';
import { useSessao } from '@/sessao/sessao';

import { requisitar } from './cliente';
import { queryClient } from './consultas';

// O contrato ainda não fixa o corpo do login: { email, senha } → { token } até o OpenAPI do Core (F1.7).
export async function entrar(email: string, senha: string) {
  const { token } = await requisitar<{ token: string }>('/auth/login', { metodo: 'POST', corpo: { email, senha } });
  await useSessao.getState().definir(token);
}

export async function sair() {
  await useSessao.getState().encerrar();
  queryClient.clear();
  await limparCache();
  await usePreferencias.getState().definirModoIdoso(null);
}
