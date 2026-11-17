// Portas de persistência local. As telas leem daqui (local-first); o servidor só atualiza o que está aqui.

import type { AlertaDeCalor } from '@/domain/alerta/alerta-de-calor';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

import type { ContatoDaRede, Domicilio, Leitura, Morador } from '../modelos';

export interface CasaRepository {
  // Retrato vindo do servidor: atualiza as que vieram e remove as que saíram da rede de cuidado.
  substituirTodas(casas: Domicilio[]): Promise<void>;
  salvar(casa: Domicilio): Promise<void>;
  listar(): Promise<Domicilio[]>;
  buscar(id: string): Promise<Domicilio | null>;
  registrarLeitura(id: string, leitura: Leitura, nivel: NivelDeRisco): Promise<void>;
  definirSentinelaOnline(id: string, deviceId: string, online: boolean): Promise<void>;
  registrarCheckIn(id: string, momento: string): Promise<void>;
  definirAlertaAberto(id: string, alertaId: string | null): Promise<void>;
}

export interface LeituraRepository {
  inserir(domicilioId: string, leituras: Leitura[]): Promise<void>;
  // Da mais antiga para a mais recente.
  desde(domicilioId: string, inicio: Date): Promise<Leitura[]>;
  podarAntesDe(limite: Date): Promise<number>;
}

export type AlertaSalvo = { alerta: AlertaDeCalor; apelidoDaCasa: string };

export interface AlertaRepository {
  salvar(alerta: AlertaDeCalor, apelidoDaCasa: string): Promise<void>;
  substituirTodos(alertas: AlertaSalvo[]): Promise<void>;
  buscar(id: string): Promise<AlertaSalvo | null>;
  // Ativos primeiro, depois do mais recente para o mais antigo.
  listar(): Promise<AlertaSalvo[]>;
}

export interface MoradorRepository {
  substituirDaCasa(domicilioId: string, moradores: Morador[]): Promise<void>;
  listarDaCasa(domicilioId: string): Promise<Morador[]>;
}

export interface RedeRepository {
  substituirDaCasa(domicilioId: string, contatos: ContatoDaRede[]): Promise<void>;
  listarDaCasa(domicilioId: string): Promise<ContatoDaRede[]>;
}

export interface PreferenciaRepository {
  ler(chave: string): Promise<string | null>;
  gravar(chave: string, valor: string | null): Promise<void>;
}

// Quando cada recurso foi atualizado pelo servidor pela última vez ("dados de 14:32").
export interface SincronizacaoRepository {
  marcar(recurso: string, momento: Date): Promise<void>;
  quando(recurso: string): Promise<string | null>;
}
