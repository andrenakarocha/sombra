import type { Alerta } from '@/api/tipos';

import { banco } from './banco';
import type { Cacheado } from './casas';

export async function salvarAlertas(alertas: Alerta[]) {
  const db = await banco();
  const agora = new Date().toISOString();
  await db.withTransactionAsync(async () => {
    await db.runAsync('DELETE FROM alertas');
    for (const alerta of alertas) {
      await db.runAsync(
        'INSERT INTO alertas (id, domicilio_id, dados, atualizado_em) VALUES (?, ?, ?, ?)',
        alerta.id,
        alerta.domicilioId,
        JSON.stringify(alerta),
        agora,
      );
    }
  });
}

export async function lerAlertas(): Promise<Cacheado<Alerta[]> | null> {
  const db = await banco();
  const linhas = await db.getAllAsync<{ dados: string; atualizado_em: string }>(
    'SELECT dados, atualizado_em FROM alertas',
  );
  if (linhas.length === 0) return null;
  return { dados: linhas.map((l) => JSON.parse(l.dados) as Alerta), atualizadoEm: linhas[0]!.atualizado_em };
}
