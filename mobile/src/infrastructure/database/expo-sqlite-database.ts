import * as SQLite from 'expo-sqlite';

import type { SqlDatabase, SqlValue } from './sql-database';

export class ExpoSqliteDatabase implements SqlDatabase {
  private profundidade = 0;

  private constructor(private readonly db: SQLite.SQLiteDatabase) {}

  static async abrir(nome = 'sombra.db') {
    const db = await SQLite.openDatabaseAsync(nome);
    await db.execAsync('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;');
    return new ExpoSqliteDatabase(db);
  }

  exec(sql: string) {
    return this.db.execAsync(sql);
  }

  async run(sql: string, params: readonly SqlValue[] = []) {
    const r = await this.db.runAsync(sql, params as SqlValue[]);
    return { changes: r.changes };
  }

  all<T>(sql: string, params: readonly SqlValue[] = []) {
    return this.db.getAllAsync<T>(sql, params as SqlValue[]);
  }

  first<T>(sql: string, params: readonly SqlValue[] = []) {
    return this.db.getFirstAsync<T>(sql, params as SqlValue[]);
  }

  // Transação aninhada vira parte da de fora: um caso de uso pode juntar vários repositórios numa só.
  async transaction(trabalho: () => Promise<void>) {
    if (this.profundidade > 0) return trabalho();
    this.profundidade++;
    await this.db.execAsync('BEGIN');
    try {
      await trabalho();
      await this.db.execAsync('COMMIT');
    } catch (erro) {
      await this.db.execAsync('ROLLBACK');
      throw erro;
    } finally {
      this.profundidade--;
    }
  }
}
