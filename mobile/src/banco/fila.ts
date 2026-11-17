import type { SituacaoDoCheckIn } from '@/api/tipos';

import { banco } from './banco';

export type CheckInNaFila = { id: number; domicilioId: string; situacao: SituacaoDoCheckIn; momento: string };

export async function enfileirar(domicilioId: string, situacao: SituacaoDoCheckIn, momento: string) {
  const db = await banco();
  await db.runAsync(
    'INSERT INTO fila_checkins (domicilio_id, situacao, momento) VALUES (?, ?, ?)',
    domicilioId,
    situacao,
    momento,
  );
}

export async function pendentes(): Promise<CheckInNaFila[]> {
  const db = await banco();
  return db.getAllAsync<CheckInNaFila>(
    'SELECT id, domicilio_id AS domicilioId, situacao, momento FROM fila_checkins ORDER BY id',
  );
}

export async function remover(id: number) {
  const db = await banco();
  await db.runAsync('DELETE FROM fila_checkins WHERE id = ?', id);
}
