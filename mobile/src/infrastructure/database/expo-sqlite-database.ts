import * as SQLite from 'expo-sqlite';

import { BancoSerializado, type SqlValue } from './sql-database';

export async function abrirBancoDoAparelho(nome = 'sombra.db') {
  const db = await SQLite.openDatabaseAsync(nome);
  await db.execAsync('PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;');
  return new BancoSerializado({
    exec: (sql) => db.execAsync(sql),
    run: async (sql, params) => ({ changes: (await db.runAsync(sql, params as SqlValue[])).changes }),
    all: (sql, params) => db.getAllAsync(sql, params as SqlValue[]),
  });
}
