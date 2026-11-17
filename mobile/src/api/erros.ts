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
