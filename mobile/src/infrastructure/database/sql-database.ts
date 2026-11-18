// Porta mínima de SQL. No aparelho é o expo-sqlite; no Jest é o sql.js (SQLite de verdade em WASM),
// assim os repositórios são testados com as mesmas queries que rodam no celular.

export type SqlValue = string | number | null;

export interface SqlDatabase {
  exec(sql: string): Promise<void>;
  run(sql: string, params?: readonly SqlValue[]): Promise<{ changes: number }>;
  all<T>(sql: string, params?: readonly SqlValue[]): Promise<T[]>;
  first<T>(sql: string, params?: readonly SqlValue[]): Promise<T | null>;
  // Uma transação por vez (as outras esperam), com um `tx` explícito: o que roda dentro usa o `tx`, e uma
  // transação pedida pelo `tx` vira parte da de fora. Sem isso, duas cadeias assíncronas dividiriam a mesma
  // transação e um ROLLBACK de uma desfaria a escrita da outra.
  // Dentro do trabalho, use só o `tx`: usar o banco de fora espera a vez atrás da própria transação (trava).
  transaction<T>(trabalho: (tx: SqlDatabase) => Promise<T>): Promise<T>;
}

export const SqlBool = {
  de: (valor: boolean) => (valor ? 1 : 0),
  para: (valor: number) => valor === 1,
} as const;

type Bruto = {
  exec(sql: string): Promise<void>;
  run(sql: string, params: readonly SqlValue[]): Promise<{ changes: number }>;
  all<T>(sql: string, params: readonly SqlValue[]): Promise<T[]>;
};

// Mutex + transação explícita, igual para os dois adaptadores: cada um só sabe executar um comando.
export class BancoSerializado implements SqlDatabase {
  private fila: Promise<unknown> = Promise.resolve();

  constructor(private readonly bruto: Bruto) {}

  private emFila<T>(trabalho: () => Promise<T>): Promise<T> {
    const vez = this.fila.then(trabalho, trabalho);
    this.fila = vez.catch(() => {});
    return vez;
  }

  exec(sql: string) {
    return this.emFila(() => this.bruto.exec(sql));
  }

  run(sql: string, params: readonly SqlValue[] = []) {
    return this.emFila(() => this.bruto.run(sql, params));
  }

  all<T>(sql: string, params: readonly SqlValue[] = []) {
    return this.emFila(() => this.bruto.all<T>(sql, params));
  }

  async first<T>(sql: string, params: readonly SqlValue[] = []) {
    return (await this.all<T>(sql, params))[0] ?? null;
  }

  transaction<T>(trabalho: (tx: SqlDatabase) => Promise<T>): Promise<T> {
    return this.emFila(async () => {
      const bruto = this.bruto;
      const tx: SqlDatabase = {
        exec: (sql) => bruto.exec(sql),
        run: (sql, params = []) => bruto.run(sql, params),
        all: (sql, params = []) => bruto.all(sql, params),
        first: async (sql, params = []) => (await bruto.all<never>(sql, params))[0] ?? null,
        transaction: (dentro) => dentro(tx),
      };
      await bruto.exec('BEGIN');
      try {
        const resultado = await trabalho(tx);
        await bruto.exec('COMMIT');
        return resultado;
      } catch (erro) {
        await bruto.exec('ROLLBACK');
        throw erro;
      }
    });
  }
}
