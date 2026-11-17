// Tipos do contrato REST do Core (docs/contratos/core-api.md). Na F1.7 o OpenAPI do Core vira a fonte da verdade.
// O contrato ainda não detalha Alerta, Morador e a rede de cuidado: os campos marcados com "rascunho" são a leitura do app.

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

export type Pagina<T> = { itens: T[]; pagina: number; tamanho: number; total: number };

// rascunho
export type EstadoDoAlerta = 'AGUARDANDO_CHECKIN' | 'ESCALONADO' | 'RECONHECIDO' | 'RESOLVIDO';

export type AcaoDeReconhecimento = 'VOU_LIGAR' | 'ESTOU_INDO' | 'ELA_ESTA_BEM';

// rascunho: a linha do tempo dos degraus (GET /alertas/{id})
export type EventoDoAlerta = { momento: string; degrau: number; descricao: string };

// rascunho
export type Alerta = {
  id: string;
  domicilioId: string;
  apelidoDaCasa: string;
  tipo: AlertaResumo['tipo'];
  estado: EstadoDoAlerta;
  nivel: NivelDeRisco;
  degrau: number;
  abertoEm: string;
  prazo: string;
  linhaDoTempo?: EventoDoAlerta[];
};

// rascunho: o telefone chega sem máscara só para o Cuidador da própria casa (spec 07)
export type Morador = {
  id: string;
  nome: string;
  idade: number;
  telefone: string | null;
  cpf: string;
  cartaoSus: string;
  perfilDeSaude: string;
  fatoresDeRisco: string[];
};

// rascunho
export type ContatoDaRede = { id: string; nome: string; papel: 'FAMILIAR' | 'VIZINHO'; telefone: string };
