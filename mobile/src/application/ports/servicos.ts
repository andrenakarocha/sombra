// Serviços do aparelho que a aplicação usa sem saber como são feitos.

import type { OutboxRepository } from './outbox';
import type {
  AlertaRepository,
  CasaRepository,
  LeituraRepository,
  MoradorRepository,
  RedeRepository,
  SincronizacaoRepository,
} from './repositorios';

export type RepositoriosDaUnidade = {
  casas: CasaRepository;
  leituras: LeituraRepository;
  alertas: AlertaRepository;
  moradores: MoradorRepository;
  rede: RedeRepository;
  outbox: OutboxRepository;
  sincronizacao: SincronizacaoRepository;
};

export interface CofreDeSessao {
  ler(): Promise<string | null>;
  salvar(token: string): Promise<void>;
  apagar(): Promise<void>;
}

// Unit of Work: o trabalho recebe repositórios presos a uma transação só (uma por vez no SQLite).
export interface UnidadeDeTrabalho {
  executar<T>(trabalho: (repos: RepositoriosDaUnidade) => Promise<T>): Promise<T>;
}

export type Canal = 'lembretes' | 'alertas';
export type Notificacao = { canal: Canal; titulo: string; corpo: string };

export interface Notificador {
  configurar(): Promise<void>;
  notificar(n: Notificacao, umaPorVez: boolean): Promise<void>;
}

export interface CanalDeTempoReal {
  // Abre o hub e devolve a função que fecha.
  conectar(token: string, aoEvento: (evento: string, payload: Record<string, unknown>) => void, aoReconectar: () => void): () => void;
}

export interface MonitorDeRede {
  aoMudar(ouvinte: (online: boolean) => void): () => void;
}

// O sistema decide se e quando o worker roda; sem garantia, o app também drena em primeiro plano.
export type EstadoDoSegundoPlano = 'ATIVO' | 'RESTRITO' | 'INDISPONIVEL';

export interface SincronizacaoEmSegundoPlano {
  registrar(): Promise<EstadoDoSegundoPlano>;
}

export type Coordenadas = { lat: number; lon: number };

export interface Localizacao {
  // null: sem permissão ou sem sinal (a tela segue com a distância que o servidor mandou).
  atual(): Promise<Coordenadas | null>;
  // Direção do aparelho em graus (bússola); devolve a função que para de ouvir.
  observarDirecao(ouvinte: (graus: number) => void): Promise<() => void>;
}
