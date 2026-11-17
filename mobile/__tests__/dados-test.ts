import { ErroDaApi, requisitar, SemConexao } from '@/api/cliente';
import { comCache } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import { MIGRACOES, migrar } from '@/banco/banco';
import { CASAS } from '@/demo/fixtures';
import { useSessao } from '@/sessao/sessao';

jest.mock('expo-sqlite', () => ({ openDatabaseAsync: jest.fn() }));
jest.mock('expo-network', () => ({ addNetworkStateListener: () => ({ remove: () => {} }) }));
jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async () => null),
  setItemAsync: jest.fn(async () => {}),
  deleteItemAsync: jest.fn(async () => {}),
}));

const resposta = (status: number, corpo: unknown) =>
  ({ ok: status < 400, status, headers: new Headers(), json: async () => corpo }) as Response;

describe('requisitar', () => {
  afterEach(() => useSessao.setState({ token: null }));

  it('manda o token e um X-Correlation-Id de 16 hex', async () => {
    useSessao.setState({ token: 'tok' });
    const fetchMock = jest.fn(async () => resposta(200, { ok: 1 }));
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    await requisitar('/domicilios');
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(url).toBe('http://localhost:5080/api/v1/domicilios');
    expect(headers.Authorization).toBe('Bearer tok');
    expect(headers['X-Correlation-Id']).toMatch(/^[0-9a-f]{16}$/);
  });

  it('sem rede vira SemConexao', async () => {
    globalThis.fetch = jest.fn(async () => {
      throw new TypeError('Network request failed');
    });
    await expect(requisitar('/domicilios')).rejects.toBeInstanceOf(SemConexao);
  });

  it('ProblemDetails vira ErroDaApi com o código curto', async () => {
    globalThis.fetch = jest.fn(async () => resposta(422, { title: 'Regra de negócio violada', correlationId: '4f1c2b7e9a0d4e6f' }));
    const erro = (await requisitar('/alertas/x/reconhecer', { metodo: 'POST' }).catch((e: unknown) => e)) as ErroDaApi;
    expect(erro).toBeInstanceOf(ErroDaApi);
    expect(erro.codigo).toBe('4f1c2b7e');
    expect(mensagemDeErro(erro)).toBe('Regra de negócio violada (código do erro: 4f1c2b7e)');
  });

  it('401 com token encerra a sessão', async () => {
    useSessao.setState({ token: 'vencido' });
    globalThis.fetch = jest.fn(async () => resposta(401, { title: 'Não autorizado' }));
    await expect(requisitar('/domicilios')).rejects.toBeInstanceOf(ErroDaApi);
    expect(useSessao.getState().token).toBeNull();
  });
});

describe('comCache (abre offline com os últimos dados)', () => {
  const cache = { dados: CASAS, atualizadoEm: '2026-11-18T17:00:00Z' };

  it('com a API no ar, salva no SQLite e devolve origem api', async () => {
    const salvar = jest.fn(async () => {});
    const r = await comCache(async () => CASAS, salvar, async () => null);
    expect(salvar).toHaveBeenCalledWith(CASAS);
    expect(r.origem).toBe('api');
  });

  it.each([
    ['sem rede', new SemConexao()],
    ['API com erro 503', new ErroDaApi(503, 'Indisponível', 'abc')],
  ])('%s: devolve o cache', async (_, erro) => {
    const r = await comCache(
      async () => {
        throw erro;
      },
      async () => {},
      async () => cache,
    );
    expect(r).toEqual({ ...cache, origem: 'cache' });
  });

  it('erro do cliente (4xx) não esconde atrás do cache', async () => {
    const erro = new ErroDaApi(403, 'Proibido', 'abc');
    await expect(
      comCache(
        async () => {
          throw erro;
        },
        async () => {},
        async () => cache,
      ),
    ).rejects.toBe(erro);
  });

  it('sem rede e sem cache, propaga o erro', async () => {
    await expect(
      comCache(
        async () => {
          throw new SemConexao();
        },
        async () => {},
        async () => null,
      ),
    ).rejects.toBeInstanceOf(SemConexao);
  });
});

describe('migrar', () => {
  function bancoFalso(versao: number) {
    const executados: string[] = [];
    return {
      executados,
      getFirstAsync: async () => ({ user_version: versao }),
      execAsync: async (sql: string) => {
        executados.push(sql);
      },
      withTransactionAsync: async (fn: () => Promise<void>) => fn(),
    };
  }

  it('banco novo: aplica todas as migrações em ordem', async () => {
    const db = bancoFalso(0);
    await migrar(db as never);
    expect(db.executados).toContain(MIGRACOES[0]);
    expect(db.executados.at(-1)).toBe(`PRAGMA user_version = ${MIGRACOES.length}`);
  });

  it('banco em dia: não roda nada', async () => {
    const db = bancoFalso(MIGRACOES.length);
    await migrar(db as never);
    expect(db.executados).toEqual([]);
  });
});
