// Dados fictícios (personas do plano, nenhum dado pessoal real).

import type { Domicilio } from '@/api/tipos';

export const CASAS: Domicilio[] = [
  {
    id: '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10',
    apelido: 'Casa da Dona Lurdes',
    bairro: 'Santana',
    regiaoClimatica: 'SP-MIRANTE',
    nivelAtual: 'CAUTELA_EXTREMA',
    ultimaLeitura: { momento: '2026-11-18T18:00:00Z', temperaturaC: 33.4, umidadePct: 40, indiceDeCalorC: 34.4 },
    sentinela: { deviceId: 'snt-7f3a9c', online: true },
    ultimoCheckIn: '2026-11-18T12:02:00Z',
    alertaAberto: {
      id: '5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55',
      tipo: 'DE_SENTINELA',
      estado: 'AGUARDANDO_CHECKIN',
      degrau: 0,
      prazo: '2026-11-18T18:30:00Z',
    },
  },
];
