// Porta mínima de SQL. No aparelho é o expo-sqlite; no Jest é o sql.js (SQLite de verdade em WASM),
// assim os repositórios são testados com as mesmas queries que rodam no celular.

export type SqlValue = string | number | null;

export interface SqlDatabase {
  exec(sql: string): Promise<void>;
  run(sql: string, params?: readonly SqlValue[]): Promise<{ changes: number }>;
  all<T>(sql: string, params?: readonly SqlValue[]): Promise<T[]>;
  first<T>(sql: string, params?: readonly SqlValue[]): Promise<T | null>;
  transaction(trabalho: () => Promise<void>): Promise<void>;
}

export const SqlBool = {
  de: (valor: boolean) => (valor ? 1 : 0),
  para: (valor: number) => valor === 1,
} as const;
