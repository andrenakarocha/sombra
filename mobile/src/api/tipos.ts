// Tipos do contrato REST do Core (docs/contratos/core-api.md). Na F1.7 o OpenAPI do Core vira a fonte da verdade.

import type { NivelDeRisco } from '@/dominio/nivel';

export type Leitura = {
  momento: string;
  temperaturaC: number;
  umidadePct: number;
  indiceDeCalorC: number;
};

export type AlertaResumo = {
  id: string;
  tipo: 'PREVENTIVO' | 'DE_SENTINELA';
  estado: string;
  degrau: number;
  prazo: string;
};

export type Domicilio = {
  id: string;
  apelido: string;
  bairro: string;
  regiaoClimatica: string;
  nivelAtual: NivelDeRisco;
  ultimaLeitura: Leitura | null;
  sentinela: { deviceId: string; online: boolean } | null;
  ultimoCheckIn: string | null;
  alertaAberto: AlertaResumo | null;
};
