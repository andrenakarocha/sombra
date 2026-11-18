import type { Leitura } from '@/application/modelos';
import type { LeituraRepository } from '@/application/ports/repositorios';

import type { SqlDatabase } from '../sql-database';

type Linha = { momento: string; temperatura_c: number; umidade_pct: number; indice_de_calor_c: number };

export class LeituraRepositorySqlite implements LeituraRepository {
  constructor(private readonly db: SqlDatabase) {}

  inserir(domicilioId: string, leituras: Leitura[]) {
    return this.db.transaction(async (tx) => {
      for (const l of leituras) {
        await tx.run(
          `INSERT OR REPLACE INTO leituras (domicilio_id, momento, temperatura_c, umidade_pct, indice_de_calor_c)
           VALUES (?, ?, ?, ?, ?)`,
          [domicilioId, l.momento, l.temperaturaC, l.umidadePct, l.indiceDeCalorC],
        );
      }
    });
  }

  async desde(domicilioId: string, inicio: Date) {
    const linhas = await this.db.all<Linha>(
      `SELECT momento, temperatura_c, umidade_pct, indice_de_calor_c FROM leituras
       WHERE domicilio_id = ? AND momento >= ? ORDER BY momento`,
      [domicilioId, inicio.toISOString()],
    );
    return linhas.map((l) => ({
      momento: l.momento,
      temperaturaC: l.temperatura_c,
      umidadePct: l.umidade_pct,
      indiceDeCalorC: l.indice_de_calor_c,
    }));
  }

  // O aparelho guarda só o que o gráfico usa: leituras velhas saem, menos a última de cada casa.
  async podarAntesDe(limite: Date) {
    const r = await this.db.run(
      `DELETE FROM leituras WHERE momento < ?
         AND momento <> (SELECT MAX(momento) FROM leituras l2 WHERE l2.domicilio_id = leituras.domicilio_id)`,
      [limite.toISOString()],
    );
    return r.changes;
  }
}
