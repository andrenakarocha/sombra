// SQLite de verdade (sql.js, WASM) atrás da mesma porta do aparelho: os repositórios rodam as queries reais no Jest.

import initSqlJs, { type Database } from 'sql.js';

import type { SqlDatabase, SqlValue } from '@/infrastructure/database/sql-database';

export class SqlJsDatabase implements SqlDatabase {
  private profundidade = 0;

  private constructor(private readonly db: Database) {}

  static async abrir() {
    const SQL = await initSqlJs();
    const db = new SQL.Database();
    db.exec('PRAGMA foreign_keys = ON;');
    return new SqlJsDatabase(db);
  }

  async exec(sql: string) {
    this.db.exec(sql);
  }

  async run(sql: string, params: readonly SqlValue[] = []) {
    this.db.run(sql, params as SqlValue[]);
    return { changes: this.db.getRowsModified() };
  }

  async all<T>(sql: string, params: readonly SqlValue[] = []) {
    const stmt = this.db.prepare(sql, params as SqlValue[]);
    const linhas: T[] = [];
    while (stmt.step()) linhas.push(stmt.getAsObject() as T);
    stmt.free();
    return linhas;
  }

  async first<T>(sql: string, params: readonly SqlValue[] = []) {
    return (await this.all<T>(sql, params))[0] ?? null;
  }

  async transaction(trabalho: () => Promise<void>) {
    if (this.profundidade > 0) return trabalho();
    this.profundidade++;
    this.db.exec('BEGIN');
    try {
      await trabalho();
      this.db.exec('COMMIT');
    } catch (erro) {
      this.db.exec('ROLLBACK');
      throw erro;
    } finally {
      this.profundidade--;
    }
  }
}
