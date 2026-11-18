// SQLite de verdade (sql.js, WASM) atrás da mesma porta do aparelho: os repositórios rodam as queries reais no Jest.

import initSqlJs from 'sql.js';

import { BancoSerializado, type SqlValue } from '@/infrastructure/database/sql-database';

export const SqlJsDatabase = {
  async abrir() {
    const SQL = await initSqlJs();
    const db = new SQL.Database();
    db.exec('PRAGMA foreign_keys = ON;');
    return new BancoSerializado({
      exec: async (sql) => void db.exec(sql),
      run: async (sql, params) => {
        db.run(sql, params as SqlValue[]);
        return { changes: db.getRowsModified() };
      },
      all: async <T,>(sql: string, params: readonly SqlValue[]) => {
        const stmt = db.prepare(sql, params as SqlValue[]);
        const linhas: T[] = [];
        while (stmt.step()) linhas.push(stmt.getAsObject() as T);
        stmt.free();
        return linhas;
      },
    });
  },
};
