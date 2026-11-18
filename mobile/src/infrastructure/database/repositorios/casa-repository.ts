import type { Domicilio, Leitura } from '@/application/modelos';
import type { CasaRepository } from '@/application/ports/repositorios';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

import { SqlBool, type SqlDatabase } from '../sql-database';

type Linha = {
  id: string;
  apelido: string;
  bairro: string;
  regiao_climatica: string;
  nivel_atual: NivelDeRisco;
  sentinela_device_id: string | null;
  sentinela_online: number | null;
  ultimo_checkin: string | null;
  alerta_aberto_id: string | null;
  alerta_tipo: 'PREVENTIVO' | 'DE_SENTINELA' | null;
  alerta_estado: string | null;
  alerta_degrau: number | null;
  alerta_prazo: string | null;
  l_momento: string | null;
  l_temperatura: number | null;
  l_umidade: number | null;
  l_indice: number | null;
  a_id: string | null;
  a_tipo: 'PREVENTIVO' | 'DE_SENTINELA' | null;
  a_estado: string | null;
  a_degrau: number | null;
  a_prazo: string | null;
};

// A última leitura e o alerta aberto vêm das próprias tabelas (nada duplicado na linha da casa).
const SELECT = `
  SELECT c.*, l.momento AS l_momento, l.temperatura_c AS l_temperatura, l.umidade_pct AS l_umidade,
         l.indice_de_calor_c AS l_indice,
         a.id AS a_id, a.tipo AS a_tipo, a.estado AS a_estado, a.degrau AS a_degrau, a.prazo AS a_prazo
  FROM casas c
  LEFT JOIN leituras l ON l.domicilio_id = c.id
    AND l.momento = (SELECT MAX(momento) FROM leituras WHERE domicilio_id = c.id)
  LEFT JOIN alertas a ON a.id = c.alerta_aberto_id`;

function alertaAberto(l: Linha): Domicilio['alertaAberto'] {
  if (l.a_id !== null) {
    return l.a_estado === 'RESOLVIDO' ? null : { id: l.a_id, tipo: l.a_tipo!, estado: l.a_estado!, degrau: l.a_degrau!, prazo: l.a_prazo! };
  }
  if (l.alerta_aberto_id === null) return null;
  return { id: l.alerta_aberto_id, tipo: l.alerta_tipo!, estado: l.alerta_estado!, degrau: l.alerta_degrau!, prazo: l.alerta_prazo! };
}

function paraCasa(l: Linha): Domicilio {
  return {
    id: l.id,
    apelido: l.apelido,
    bairro: l.bairro,
    regiaoClimatica: l.regiao_climatica,
    nivelAtual: l.nivel_atual,
    ultimaLeitura:
      l.l_momento === null
        ? null
        : { momento: l.l_momento, temperaturaC: l.l_temperatura!, umidadePct: l.l_umidade!, indiceDeCalorC: l.l_indice! },
    sentinela: l.sentinela_device_id === null ? null : { deviceId: l.sentinela_device_id, online: SqlBool.para(l.sentinela_online ?? 0) },
    ultimoCheckIn: l.ultimo_checkin,
    alertaAberto: alertaAberto(l),
  };
}

export class CasaRepositorySqlite implements CasaRepository {
  constructor(private readonly db: SqlDatabase) {}

  private async gravar(casa: Domicilio) {
    await this.db.run(
      `INSERT INTO casas (id, apelido, bairro, regiao_climatica, nivel_atual, sentinela_device_id, sentinela_online,
                          ultimo_checkin, alerta_aberto_id, alerta_tipo, alerta_estado, alerta_degrau, alerta_prazo,
                          atualizado_em)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
       ON CONFLICT (id) DO UPDATE SET apelido = excluded.apelido, bairro = excluded.bairro,
         regiao_climatica = excluded.regiao_climatica, nivel_atual = excluded.nivel_atual,
         sentinela_device_id = excluded.sentinela_device_id, sentinela_online = excluded.sentinela_online,
         ultimo_checkin = excluded.ultimo_checkin, alerta_aberto_id = excluded.alerta_aberto_id,
         alerta_tipo = excluded.alerta_tipo, alerta_estado = excluded.alerta_estado,
         alerta_degrau = excluded.alerta_degrau, alerta_prazo = excluded.alerta_prazo,
         atualizado_em = excluded.atualizado_em`,
      [
        casa.id,
        casa.apelido,
        casa.bairro,
        casa.regiaoClimatica,
        casa.nivelAtual,
        casa.sentinela?.deviceId ?? null,
        casa.sentinela ? SqlBool.de(casa.sentinela.online) : null,
        casa.ultimoCheckIn,
        casa.alertaAberto?.id ?? null,
        casa.alertaAberto?.tipo ?? null,
        casa.alertaAberto?.estado ?? null,
        casa.alertaAberto?.degrau ?? null,
        casa.alertaAberto?.prazo ?? null,
        new Date().toISOString(),
      ],
    );
    if (casa.ultimaLeitura) await this.inserirLeitura(casa.id, casa.ultimaLeitura);
  }

  private inserirLeitura(id: string, l: Leitura) {
    return this.db.run(
      `INSERT OR REPLACE INTO leituras (domicilio_id, momento, temperatura_c, umidade_pct, indice_de_calor_c)
       VALUES (?, ?, ?, ?, ?)`,
      [id, l.momento, l.temperaturaC, l.umidadePct, l.indiceDeCalorC],
    );
  }

  substituirTodas(casas: Domicilio[]) {
    return this.db.transaction(async (tx) => {
      const r = new CasaRepositorySqlite(tx);
      for (const casa of casas) await r.gravar(casa);
      const ids = casas.map((c) => c.id);
      await tx.run(
        `DELETE FROM casas WHERE id NOT IN (${ids.map(() => '?').join(', ') || "''"})`,
        ids,
      );
    });
  }

  salvar(casa: Domicilio) {
    return this.db.transaction((tx) => new CasaRepositorySqlite(tx).gravar(casa));
  }

  async listar() {
    return (await this.db.all<Linha>(`${SELECT} ORDER BY c.apelido`)).map(paraCasa);
  }

  async buscar(id: string) {
    const linha = await this.db.first<Linha>(`${SELECT} WHERE c.id = ?`, [id]);
    return linha && paraCasa(linha);
  }

  registrarLeitura(id: string, leitura: Leitura, nivel: NivelDeRisco) {
    return this.db.transaction(async (tx) => {
      await new CasaRepositorySqlite(tx).inserirLeitura(id, leitura);
      await tx.run('UPDATE casas SET nivel_atual = ? WHERE id = ?', [nivel, id]);
    });
  }

  async definirSentinelaOnline(id: string, deviceId: string, online: boolean) {
    await this.db.run('UPDATE casas SET sentinela_device_id = ?, sentinela_online = ? WHERE id = ?', [deviceId, SqlBool.de(online), id]);
  }

  async registrarCheckIn(id: string, momento: string) {
    // Um check-in antigo que saiu atrasado da fila não sobrescreve um mais novo.
    await this.db.run(
      'UPDATE casas SET ultimo_checkin = ? WHERE id = ? AND (ultimo_checkin IS NULL OR ultimo_checkin < ?)',
      [momento, id, momento],
    );
  }

  async definirAlertaAberto(id: string, alertaId: string | null) {
    // O resumo antigo sai junto: dali em diante vale o alerta da tabela alertas.
    await this.db.run(
      `UPDATE casas SET alerta_aberto_id = ?, alerta_tipo = NULL, alerta_estado = NULL, alerta_degrau = NULL,
         alerta_prazo = NULL WHERE id = ?`,
      [alertaId, id],
    );
  }
}
