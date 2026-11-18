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

// A resposta chegou, mas não bate com o contrato (schema Zod): não entra no domínio.
export class RespostaForaDoContrato extends Error {
  constructor(
    readonly rota: string,
    readonly detalhe: string,
  ) {
    super(`Resposta fora do contrato em ${rota}`);
  }
}

// Para consultas: só "fora do ar" cai no dado local. Sessão vencida e erro de regra aparecem para a pessoa.
export function foraDoAr(erro: unknown) {
  return erro instanceof SemConexao || (erro instanceof ErroDaApi && erro.status >= 500);
}
