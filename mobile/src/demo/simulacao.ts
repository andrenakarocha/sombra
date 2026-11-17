import { queryClient } from '@/api/consultas';
import { receberEvento } from '@/api/tempo-real';

import { escalonarNaDemo, reiniciarDemo, simularOndaDeCalorNaDemo } from './api-demo';

const ESPERA_DO_DEGRAU_MS = 8000;

// O mesmo caminho dos eventos do hub: o card fica vermelho ao vivo e, sem resposta, chega o lembrete leve (D13).
export function simularOndaDeCalor() {
  const { domicilioId, alertaId, leitura, prazo } = simularOndaDeCalorNaDemo();
  receberEvento('LeituraRegistrada', { domicilioId, ...leitura, nivel: 'PERIGO' });
  receberEvento('AlertaAberto', { alertaId, domicilioId, tipo: 'DE_SENTINELA', nivel: 'PERIGO', prazo });
  setTimeout(() => {
    const escalonado = escalonarNaDemo(alertaId);
    if (escalonado) receberEvento('AlertaEscalonado', escalonado);
  }, ESPERA_DO_DEGRAU_MS);
}

export function recomecarDemo() {
  reiniciarDemo();
  queryClient.invalidateQueries();
}
