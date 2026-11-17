// Banco local (expo-sqlite). As migrações são versionadas pelo PRAGMA user_version: a posição na lista é a versão.

import * as SQLite from 'expo-sqlite';

export const MIGRACOES: string[] = [
  // 1 · cache das casas, para o app abrir offline com os últimos dados
  `CREATE TABLE casas (
     id TEXT PRIMARY KEY NOT NULL,
     dados TEXT NOT NULL,
     atualizado_em TEXT NOT NULL
   );`,
];

type BancoMigravel = Pick<SQLite.SQLiteDatabase, 'getFirstAsync' | 'execAsync' | 'withTransactionAsync'>;

export async function migrar(db: BancoMigravel) {
  const linha = await db.getFirstAsync<{ user_version: number }>('PRAGMA user_version');
  for (let versao = linha?.user_version ?? 0; versao < MIGRACOES.length; versao++) {
    await db.withTransactionAsync(async () => {
      await db.execAsync(MIGRACOES[versao]!);
      await db.execAsync(`PRAGMA user_version = ${versao + 1}`);
    });
  }
}

let aberto: Promise<SQLite.SQLiteDatabase> | null = null;

export function banco() {
  aberto ??= SQLite.openDatabaseAsync('sombra.db').then(async (db) => {
    await db.execAsync('PRAGMA journal_mode = WAL');
    await migrar(db);
    return db;
  });
  return aberto;
}
