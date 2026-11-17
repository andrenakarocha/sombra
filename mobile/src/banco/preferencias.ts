import { banco } from './banco';

export async function lerPreferencia(chave: string): Promise<string | null> {
  const db = await banco();
  const linha = await db.getFirstAsync<{ valor: string }>('SELECT valor FROM preferencias WHERE chave = ?', chave);
  return linha?.valor ?? null;
}

export async function gravarPreferencia(chave: string, valor: string | null) {
  const db = await banco();
  if (valor === null) await db.runAsync('DELETE FROM preferencias WHERE chave = ?', chave);
  else await db.runAsync('INSERT OR REPLACE INTO preferencias (chave, valor) VALUES (?, ?)', chave, valor);
}
