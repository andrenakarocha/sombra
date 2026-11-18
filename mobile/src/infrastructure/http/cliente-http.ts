// Cliente REST do Core: correlação, timeout, Idempotency-Key e ProblemDetails → erros da aplicação.

import { ErroDaApi, SemConexao } from '@/application/erros';
import { sessaoStore } from '@/application/estado/sessao';

import { API_URL } from '../config';

const TEMPO_LIMITE_MS = 8000;

export type Opcoes = {
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  corpo?: unknown;
  idempotencyKey?: string;
};

function novoCorrelationId() {
  return Array.from({ length: 16 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
}

export class ClienteHttp {
  // `aoSessaoVencida` é ligado pelo container ao caso de uso de sessão.
  constructor(
    private readonly aoSessaoVencida: () => Promise<void>,
    private readonly base = `${API_URL}/api/v1`,
  ) {}

  async requisitar(caminho: string, { metodo = 'GET', corpo, idempotencyKey }: Opcoes = {}): Promise<unknown> {
    const token = sessaoStore.getState().token;
    const correlationId = novoCorrelationId();
    const controle = new AbortController();
    const limite = setTimeout(() => controle.abort(), TEMPO_LIMITE_MS);

    let resposta: Response;
    try {
      resposta = await fetch(`${this.base}${caminho}`, {
        method: metodo,
        headers: {
          Accept: 'application/json',
          'Content-Type': 'application/json',
          'X-Correlation-Id': correlationId,
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
          ...(idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {}),
        },
        body: corpo === undefined ? undefined : JSON.stringify(corpo),
        signal: controle.signal,
      });
    } catch {
      throw new SemConexao();
    } finally {
      clearTimeout(limite);
    }

    if (resposta.status === 401 && token) await this.aoSessaoVencida();
    if (!resposta.ok) {
      const problema = await resposta.json().catch(() => ({}));
      throw new ErroDaApi(
        resposta.status,
        problema.title ?? 'Erro inesperado',
        problema.correlationId ?? resposta.headers.get('X-Correlation-Id') ?? correlationId,
      );
    }
    if (resposta.status === 204) return undefined;
    return resposta.json();
  }
}
