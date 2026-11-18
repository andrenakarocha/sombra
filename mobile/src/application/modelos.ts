// Modelos de leitura: o que as telas mostram e o que o contrato REST do Core (docs/contratos/core-api.md) transporta.
// O contrato ainda não detalha Alerta, Morador e a rede de cuidado: os campos marcados com "rascunho" são a leitura do
// app. Na F1.7 o OpenAPI do Core vira a fonte da verdade; os schemas Zod da infraestrutura conferem cada resposta.

import type { AcaoDoCuidador, EstadoDoAlerta, EventoDoAlerta } from '@/domain/alerta/alerta-de-calor';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';
import type { FatorDeRisco } from '@/domain/morador/fator-de-risco';
import type { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';
import type { DadosDaRotina } from '@/domain/rotina/rotina';

export type { DadosDaRotina, EstadoDoAlerta, EventoDoAlerta, FatorDeRisco };
export type AcaoDeReconhecimento = AcaoDoCuidador;

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
  // Calculado no app pela máquina de estados (null: não vai haver próximo degrau).
  minutosAteProximoDegrau?: number | null;
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
  fatoresDeRisco: FatorDeRisco[];
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

// Local-first: as telas sempre leem do aparelho; `origem` diz se o servidor respondeu agora.
export type Resultado<T> = { dados: T; atualizadoEm: string | null; origem: 'servidor' | 'local' };

export type PainelDaCasa = {
  casa: Domicilio;
  leituras24h: Leitura[];
  moradores: Morador[];
  rede: ContatoDaRede[];
  // R3: risco do primeiro morador (a casa do Sombra é de quem mora sozinho).
  risco: RiscoPersonalizado | null;
};

// RECUSADO: o Core recusou de vez (foi para a dead-letter da Central de sincronização).
export type ResultadoDoEnvio = 'ENVIADO' | 'NA_FILA' | 'RECUSADO';

// rascunho: POST /domicilios/{id}/sentinelas responde com o segredo HMAC uma única vez (core-api.md).
export type SentinelaVinculada = { deviceId: string; segredo: string };
