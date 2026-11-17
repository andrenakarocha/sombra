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

export type SituacaoDoCheckIn = 'ESTOU_BEM' | 'PRECISO_DE_AJUDA';

// O contrato tem { origem, situacao }; o app manda também o momento em que a pessoa apertou (rascunho), para um
// check-in que esperou na fila offline não chegar com o horário errado.
export type CheckIn = { origem: 'APP' | 'CUIDADOR'; situacao: SituacaoDoCheckIn; momento: string };

// docs/contratos/clima-service.md (o Core repassa). Coordenadas: rascunho, para o "abrir rota".
export type PontoDeResfriamento = {
  id: string;
  nome: string;
  tipo: string;
  distanciaKm: number;
  horario: { abre: string; fecha: string };
  acessivel: boolean;
  fonte: string;
  lat?: number;
  lon?: number;
};

export type FarolDePrivacidade = 'VERDE' | 'AMARELO' | 'VERMELHO';

// rascunho: GET /domicilios/{id}/privacidade (spec 07: o farol da casa e a mensagem traduzida para o cidadão)
export type PrivacidadeDaCasa = {
  farol: FarolDePrivacidade;
  mensagens: { momento: string; mensagemCidadao: string; fonte: 'IA' | 'template' }[];
};

export type FatorDeRisco = 'IDADE_AVANCADA' | 'CARDIOPATIA' | 'USO_DE_DIURETICO';

// O que o app manda ao cadastrar ou editar um morador (os dados vão inteiros; a resposta volta mascarada).
export type NovoMorador = {
  nome: string;
  idade: number;
  cpf: string;
  cartaoSus: string;
  telefone: string;
  perfilDeSaude: string;
  fatoresDeRisco: FatorDeRisco[];
};
