// Cliente REST do Core (padrão BFF: o app só fala com o Core).

import { API_URL } from '@/config';
import { useSessao } from '@/sessao/sessao';

const TEMPO_LIMITE_MS = 8000;

export class SemConexao extends Error {
  constructor() {
    super('Sem conexão com o Sombra');
  }
}

// ProblemDetails do Core. O `detail` nunca traz PII (docs/contratos/core-api.md).
export class ErroDaApi extends Error {
  constructor(
    readonly status: number,
    readonly titulo: string,
    readonly correlationId: string,
  ) {
    super(titulo);
  }

  // Os 8 primeiros caracteres, para cruzar com o log sem expor nada (docs/observabilidade.md §3).
  get codigo() {
    return this.correlationId.slice(0, 8);
  }
}

function novoCorrelationId() {
  return Array.from({ length: 16 }, () => Math.floor(Math.random() * 16).toString(16)).join('');
}

type Opcoes = { metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE'; corpo?: unknown };

export async function requisitar<T>(caminho: string, { metodo = 'GET', corpo }: Opcoes = {}): Promise<T> {
  const { token, encerrar } = useSessao.getState();
  const correlationId = novoCorrelationId();
  const controle = new AbortController();
  const limite = setTimeout(() => controle.abort(), TEMPO_LIMITE_MS);

  let resposta: Response;
  try {
    resposta = await fetch(`${API_URL}/api/v1${caminho}`, {
      method: metodo,
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        'X-Correlation-Id': correlationId,
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: corpo === undefined ? undefined : JSON.stringify(corpo),
      signal: controle.signal,
    });
  } catch {
    throw new SemConexao();
  } finally {
    clearTimeout(limite);
  }

  if (resposta.status === 401 && token) await encerrar();
  if (!resposta.ok) {
    const problema = await resposta.json().catch(() => ({}));
    throw new ErroDaApi(
      resposta.status,
      problema.title ?? 'Erro inesperado',
      problema.correlationId ?? resposta.headers.get('X-Correlation-Id') ?? correlationId,
    );
  }
  if (resposta.status === 204) return undefined as T;
  return (await resposta.json()) as T;
}
