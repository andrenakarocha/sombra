import { ErroDaApi, RespostaForaDoContrato, SemConexao } from '@/application/erros';
import { sessaoStore } from '@/application/estado/sessao';
import { ambiente } from '@/infrastructure/config';
import { ClienteHttp } from '@/infrastructure/http/cliente-http';
import { CoreHttpGateway } from '@/infrastructure/http/core-http-gateway';
import { CASAS } from '@/infrastructure/demo/fixtures';

const resposta = (status: number, corpo: unknown, headers: Record<string, string> = {}) =>
  ({ ok: status < 400, status, headers: new Headers(headers), json: async () => corpo }) as Response;

function servidor(...respostas: Response[]) {
  const fetchMock = jest.fn(async () => respostas.shift() ?? resposta(204, undefined));
  globalThis.fetch = fetchMock as unknown as typeof fetch;
  const chamada = (i: number) => {
    const [url, init] = fetchMock.mock.calls[i] as unknown as [string, RequestInit];
    return { url, init, headers: init.headers as Record<string, string>, corpo: init.body && JSON.parse(init.body as string) };
  };
  return { fetchMock, chamada };
}

beforeEach(() => sessaoStore.setState({ token: 'tok', carregada: true }));

describe('ClienteHttp', () => {
  it('manda token, X-Correlation-Id de 16 hex e Idempotency-Key quando há', async () => {
    const { chamada } = servidor(resposta(200, { ok: 1 }));
    await new ClienteHttp(async () => {}).requisitar('/x', { metodo: 'POST', corpo: { a: 1 }, idempotencyKey: 'op-1' });
    const c = chamada(0);
    expect(c.url).toBe('http://localhost:5080/api/v1/x');
    expect(c.headers.Authorization).toBe('Bearer tok');
    expect(c.headers['X-Correlation-Id']).toMatch(/^[0-9a-f]{16}$/);
    expect(c.headers['Idempotency-Key']).toBe('op-1');
    expect(c.corpo).toEqual({ a: 1 });
  });

  it('sem token não manda Authorization; 204 devolve undefined', async () => {
    sessaoStore.setState({ token: null });
    const { chamada } = servidor(resposta(204, undefined));
    expect(await new ClienteHttp(async () => {}).requisitar('/x')).toBeUndefined();
    expect(chamada(0).headers.Authorization).toBeUndefined();
  });

  it('sem rede vira SemConexao', async () => {
    globalThis.fetch = jest.fn(async () => {
      throw new TypeError('Network request failed');
    });
    await expect(new ClienteHttp(async () => {}).requisitar('/x')).rejects.toBeInstanceOf(SemConexao);
  });

  it('ProblemDetails vira ErroDaApi; sem corpo, usa o id do header', async () => {
    servidor(resposta(422, { title: 'Regra', correlationId: '4f1c2b7e9a0d4e6f' }), resposta(500, 'x', { 'X-Correlation-Id': 'abcdef1234567890' }));
    const cliente = new ClienteHttp(async () => {});
    const e1 = (await cliente.requisitar('/a').catch((e: unknown) => e)) as ErroDaApi;
    expect([e1.status, e1.titulo, e1.codigo]).toEqual([422, 'Regra', '4f1c2b7e']);
    const e2 = (await cliente.requisitar('/b').catch((e: unknown) => e)) as ErroDaApi;
    expect([e2.titulo, e2.codigo]).toEqual(['Erro inesperado', 'abcdef12']);
  });

  it('401 com token avisa a sessão vencida', async () => {
    const vencida = jest.fn(async () => {});
    servidor(resposta(401, { title: 'Não autorizado' }));
    await expect(new ClienteHttp(vencida).requisitar('/x')).rejects.toBeInstanceOf(ErroDaApi);
    expect(vencida).toHaveBeenCalledTimes(1);
  });
});

describe('CoreHttpGateway: o contrato em Zod', () => {
  const gateway = () => new CoreHttpGateway(new ClienteHttp(async () => {}));

  it('página de casas válida passa', async () => {
    servidor(resposta(200, { itens: CASAS, pagina: 1, tamanho: 100, total: CASAS.length }));
    expect(await gateway().listarCasas()).toEqual(CASAS);
  });

  it('resposta fora do contrato não entra, e o erro não carrega valor nenhum (pode ser PII)', async () => {
    servidor(resposta(200, { ...CASAS[0], nivelAtual: 'MUITO_QUENTE', apelido: 'Casa de Fulana' }));
    const erro = (await gateway().buscarCasa('d1').catch((e: unknown) => e)) as RespostaForaDoContrato;
    expect(erro).toBeInstanceOf(RespostaForaDoContrato);
    expect(erro.rota).toBe('/domicilios/d1');
    expect(erro.detalhe).toBe('nivelAtual: invalid_value');
    expect(erro.detalhe).not.toMatch(/Fulana|MUITO_QUENTE/);
  });

  it('check-in e reconhecimento vão para as rotas certas com o operationId como Idempotency-Key', async () => {
    const { chamada } = servidor(resposta(204, undefined), resposta(204, undefined));
    await gateway().enviar({
      operationId: 'op-1',
      tipo: 'CHECK_IN',
      payload: { domicilioId: 'd1', situacao: 'ESTOU_BEM', momento: '2026-11-18T18:05:00Z' },
      criadoEm: '',
      tentativas: 0,
      proximaTentativaEm: '',
      estado: 'PENDENTE',
      ultimoErro: null,
    });
    await gateway().enviar({
      operationId: 'op-2',
      tipo: 'RECONHECER_ALERTA',
      payload: { alertaId: 'a/1', acao: 'ESTOU_INDO', momento: '2026-11-18T18:05:00Z' },
      criadoEm: '',
      tentativas: 0,
      proximaTentativaEm: '',
      estado: 'PENDENTE',
      ultimoErro: null,
    });
    expect([chamada(0).url, chamada(0).headers['Idempotency-Key'], chamada(0).corpo]).toEqual([
      'http://localhost:5080/api/v1/domicilios/d1/check-ins',
      'op-1',
      { origem: 'APP', situacao: 'ESTOU_BEM', momento: '2026-11-18T18:05:00Z' },
    ]);
    expect([chamada(1).url, chamada(1).corpo]).toEqual(['http://localhost:5080/api/v1/alertas/a%2F1/reconhecer', { acao: 'ESTOU_INDO' }]);
  });

  it('as outras rotas e o login', async () => {
    const { chamada } = servidor(
      resposta(200, { token: 't' }),
      resposta(200, { itens: [], pagina: 1, tamanho: 100, total: 0 }),
      resposta(200, []),
      resposta(200, []),
      resposta(200, { itens: [], pagina: 1, tamanho: 100, total: 0 }),
      resposta(200, []),
      resposta(200, { farol: 'VERDE', mensagens: [] }),
      resposta(200, { id: 'm', nome: 'L', idade: 80, telefone: null, cpf: '*', cartaoSus: '*', perfilDeSaude: '[x]', fatoresDeRisco: [] }),
      resposta(200, { id: 'm', nome: 'L', idade: 80, telefone: null, cpf: '*', cartaoSus: '*', perfilDeSaude: '[x]', fatoresDeRisco: [] }),
    );
    const g = gateway();
    expect(await g.entrar('a@b.c', 's')).toEqual({ token: 't' });
    await g.leituras('d1', new Date('2026-11-17T18:00:00Z'), new Date('2026-11-18T18:00:00Z'));
    await g.moradores('d1');
    await g.redeDeCuidado('d1');
    await g.listarAlertas();
    await g.pontosDeResfriamento('d1');
    await g.privacidade('d1');
    await g.salvarMorador('d1', undefined, { nome: 'L' });
    await g.salvarMorador('d1', 'm', { nome: 'L' });
    expect(chamada(1).url).toContain('/domicilios/d1/leituras?de=2026-11-17T18%3A00%3A00.000Z');
    expect([chamada(7).init.method, chamada(8).init.method, chamada(8).url]).toEqual([
      'POST',
      'PUT',
      'http://localhost:5080/api/v1/domicilios/d1/moradores/m',
    ]);
  });
});

it('ambiente: URL padrão validada', () => {
  expect(ambiente.EXPO_PUBLIC_API_URL).toBe('http://localhost:5080');
});
