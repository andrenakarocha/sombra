import type { AlertaRepository, AlertaSalvo } from '@/application/ports/repositorios';
import { AlertaDeCalor, type EstadoDoAlerta, type ResolvidoPor, type TipoDeAlerta } from '@/domain/alerta/alerta-de-calor';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';

import { SqlBool, type SqlDatabase } from '../sql-database';

type Linha = {
  id: string;
  domicilio_id: string;
  apelido_da_casa: string;
  tipo: TipoDeAlerta;
  estado: EstadoDoAlerta;
  nivel: NivelDeRisco;
  degrau: number;
  aberto_em: string;
  prazo: string;
  pedido_de_ajuda: number;
  tem_vizinho: number;
  resolvido_por: ResolvidoPor | null;
};
type LinhaDeEvento = { alerta_id: string; momento: string; degrau: number; descricao: string };

export class AlertaRepositorySqlite implements AlertaRepository {
  constructor(private readonly db: SqlDatabase) {}

  private async gravar(alerta: AlertaDeCalor, apelido: string) {
    const d = alerta.dados;
    await this.db.run(
      `INSERT OR REPLACE INTO alertas (id, domicilio_id, apelido_da_casa, tipo, estado, nivel, degrau, aberto_em, prazo,
                                       pedido_de_ajuda, tem_vizinho, resolvido_por)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [d.id, d.domicilioId, apelido, d.tipo, d.estado, d.nivel, d.degrau, d.abertoEm, d.prazo,
        SqlBool.de(d.pedidoDeAjuda), SqlBool.de(d.temVizinho), d.resolvidoPor],
    );
    await this.db.run('DELETE FROM eventos_do_alerta WHERE alerta_id = ?', [d.id]);
    for (const [ordem, e] of d.linhaDoTempo.entries()) {
      await this.db.run(
        'INSERT INTO eventos_do_alerta (alerta_id, ordem, momento, degrau, descricao) VALUES (?, ?, ?, ?, ?)',
        [d.id, ordem, e.momento, e.degrau, e.descricao],
      );
    }
  }

  private async montar(linhas: Linha[]): Promise<AlertaSalvo[]> {
    if (linhas.length === 0) return [];
    const eventos = await this.db.all<LinhaDeEvento>(
      `SELECT alerta_id, momento, degrau, descricao FROM eventos_do_alerta
       WHERE alerta_id IN (${linhas.map(() => '?').join(', ')}) ORDER BY alerta_id, ordem`,
      linhas.map((l) => l.id),
    );
    return linhas.map((l) => ({
      apelidoDaCasa: l.apelido_da_casa,
      alerta: AlertaDeCalor.restaurar({
        id: l.id,
        domicilioId: l.domicilio_id,
        tipo: l.tipo,
        estado: l.estado,
        nivel: l.nivel,
        degrau: l.degrau,
        abertoEm: l.aberto_em,
        prazo: l.prazo,
        pedidoDeAjuda: SqlBool.para(l.pedido_de_ajuda),
        temVizinho: SqlBool.para(l.tem_vizinho),
        resolvidoPor: l.resolvido_por,
        linhaDoTempo: eventos
          .filter((e) => e.alerta_id === l.id)
          .map(({ momento, degrau, descricao }) => ({ momento, degrau, descricao })),
      }),
    }));
  }

  salvar(alerta: AlertaDeCalor, apelidoDaCasa: string) {
    return this.db.transaction(() => this.gravar(alerta, apelidoDaCasa));
  }

  substituirTodos(alertas: AlertaSalvo[]) {
    return this.db.transaction(async () => {
      await this.db.exec('DELETE FROM alertas');
      for (const { alerta, apelidoDaCasa } of alertas) await this.gravar(alerta, apelidoDaCasa);
    });
  }

  async buscar(id: string) {
    const linha = await this.db.first<Linha>('SELECT * FROM alertas WHERE id = ?', [id]);
    return linha ? ((await this.montar([linha]))[0] ?? null) : null;
  }

  async listar() {
    return this.montar(
      await this.db.all<Linha>("SELECT * FROM alertas ORDER BY estado = 'RESOLVIDO', aberto_em DESC"),
    );
  }
}
