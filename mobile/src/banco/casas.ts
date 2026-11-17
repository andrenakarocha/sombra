import type { Domicilio } from '@/application/modelos';

import { banco } from './banco';

export type Cacheado<T> = { dados: T; atualizadoEm: string };

export async function salvarCasas(casas: Domicilio[]) {
  const db = await banco();
  const agora = new Date().toISOString();
  await db.withTransactionAsync(async () => {
    await db.runAsync('DELETE FROM casas');
    for (const casa of casas) {
      await db.runAsync('INSERT INTO casas (id, dados, atualizado_em) VALUES (?, ?, ?)', casa.id, JSON.stringify(casa), agora);
    }
  });
}

export async function lerCasas(): Promise<Cacheado<Domicilio[]> | null> {
  const db = await banco();
  const linhas = await db.getAllAsync<{ dados: string; atualizado_em: string }>(
    'SELECT dados, atualizado_em FROM casas',
  );
  if (linhas.length === 0) return null;
  return { dados: linhas.map((l) => JSON.parse(l.dados) as Domicilio), atualizadoEm: linhas[0]!.atualizado_em };
}

export async function salvarCasa(casa: Domicilio) {
  const db = await banco();
  await db.runAsync(
    'INSERT OR REPLACE INTO casas (id, dados, atualizado_em) VALUES (?, ?, ?)',
    casa.id,
    JSON.stringify(casa),
    new Date().toISOString(),
  );
}

export async function lerCasa(id: string): Promise<Cacheado<Domicilio> | null> {
  const db = await banco();
  const linha = await db.getFirstAsync<{ dados: string; atualizado_em: string }>(
    'SELECT dados, atualizado_em FROM casas WHERE id = ?',
    id,
  );
  return linha && { dados: JSON.parse(linha.dados) as Domicilio, atualizadoEm: linha.atualizado_em };
}

// Ao sair da conta, nada da casa de outra pessoa fica no aparelho.
export async function limparCache() {
  const db = await banco();
  await db.execAsync('DELETE FROM casas; DELETE FROM alertas;');
}
