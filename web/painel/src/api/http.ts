import type { z } from 'zod';
import { sessao } from './sessao';

/**
 * Um erro da API, com o código curto para cruzar com o log sem expor nada (observabilidade §3): a tela mostra
 * "código do erro: 4f1c2b7e", os 8 primeiros caracteres do correlationId.
 */
export class ErroDaApi extends Error {
  constructor(
    readonly status: number,
    mensagem: string,
    readonly correlationId: string,
  ) {
    super(mensagem);
    this.name = 'ErroDaApi';
  }

  get codigo(): string {
    return this.correlationId.slice(0, 8);
  }
}

function novoCorrelationId(): string {
  const bytes = new Uint8Array(8);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

/**
 * Uma chamada ao Core (mesma origem: /api), validada pelo schema; 401 encerra a sessão. Com {@code semToken}, o JWT não
 * vai junto (as sondas de saúde do Clima não precisam dele, e ele não deve sair do Core).
 */
export async function pedir<T>(caminho: string, schema: z.ZodType<T>, init: RequestInit = {}, semToken = false): Promise<T> {
  const correlationId = novoCorrelationId();
  const headers = new Headers(init.headers);
  headers.set('Accept', 'application/json');
  headers.set('X-Correlation-Id', correlationId);
  const token = semToken ? undefined : sessao.atual()?.token;
  if (token) headers.set('Authorization', `Bearer ${token}`);
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json');

  let resposta: Response;
  try {
    resposta = await fetch(caminho, { ...init, headers });
  } catch {
    throw new ErroDaApi(0, 'Sem conexão com o Sombra. Confira a rede e tente de novo.', correlationId);
  }
  if (resposta.status === 401 && token) {
    sessao.sair();
    window.location.assign('/entrar');
  }
  if (!resposta.ok) {
    const corpo = (await resposta.json().catch(() => ({}))) as { detail?: string; title?: string; correlationId?: string };
    throw new ErroDaApi(
      resposta.status,
      corpo.detail ?? corpo.title ?? 'Algo deu errado.',
      corpo.correlationId ?? resposta.headers.get('X-Correlation-Id') ?? correlationId,
    );
  }
  const lido = schema.safeParse(await resposta.json());
  if (!lido.success) {
    console.error('resposta fora do contrato', caminho, lido.error.issues);
    throw new ErroDaApi(resposta.status, 'O Core respondeu fora do contrato. Informe o código ao suporte.', correlationId);
  }
  return lido.data;
}
