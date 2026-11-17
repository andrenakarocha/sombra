import type { EstadoDaOperacao, Operacao, OutboxRepository } from '@/application/ports/outbox';

import type { SqlDatabase } from '../sql-database';

type Linha = {
  operation_id: string;
  tipo: Operacao['tipo'];
  payload: string;
  criado_em: string;
  tentativas: number;
  proxima_tentativa_em: string;
  estado: EstadoDaOperacao;
  ultimo_erro: string | null;
};

const paraOperacao = (l: Linha) =>
  ({
    operationId: l.operation_id,
    tipo: l.tipo,
    payload: JSON.parse(l.payload),
    criadoEm: l.criado_em,
    tentativas: l.tentativas,
    proximaTentativaEm: l.proxima_tentativa_em,
    estado: l.estado,
    ultimoErro: l.ultimo_erro,
  }) as Operacao;

export class OutboxRepositorySqlite implements OutboxRepository {
  constructor(private readonly db: SqlDatabase) {}

  async enfileirar(op: Pick<Operacao, 'operationId' | 'tipo' | 'payload' | 'criadoEm'>) {
    // O mesmo operationId duas vezes é a mesma operação: a segunda é ignorada.
    await this.db.run(
      `INSERT OR IGNORE INTO outbox (operation_id, tipo, payload, criado_em, proxima_tentativa_em, estado)
       VALUES (?, ?, ?, ?, ?, 'PENDENTE')`,
      [op.operationId, op.tipo, JSON.stringify(op.payload), op.criadoEm, op.criadoEm],
    );
  }

  async prontas(agora: Date) {
    const linhas = await this.db.all<Linha>(
      "SELECT * FROM outbox WHERE estado = 'PENDENTE' AND proxima_tentativa_em <= ? ORDER BY criado_em, operation_id",
      [agora.toISOString()],
    );
    return linhas.map(paraOperacao);
  }

  async marcarEnviada(operationId: string) {
    await this.db.run("UPDATE outbox SET estado = 'ENVIADO', ultimo_erro = NULL WHERE operation_id = ?", [operationId]);
  }

  async reagendar(operationId: string, tentativas: number, proxima: Date, erro: string) {
    await this.db.run('UPDATE outbox SET tentativas = ?, proxima_tentativa_em = ?, ultimo_erro = ? WHERE operation_id = ?', [
      tentativas,
      proxima.toISOString(),
      erro,
      operationId,
    ]);
  }

  async moverParaDeadLetter(operationId: string, tentativas: number, erro: string) {
    await this.db.run("UPDATE outbox SET estado = 'DEAD_LETTER', tentativas = ?, ultimo_erro = ? WHERE operation_id = ?", [
      tentativas,
      erro,
      operationId,
    ]);
  }

  async reenviar(operationId: string, agora: Date) {
    await this.db.run(
      "UPDATE outbox SET estado = 'PENDENTE', tentativas = 0, proxima_tentativa_em = ? WHERE operation_id = ? AND estado = 'DEAD_LETTER'",
      [agora.toISOString(), operationId],
    );
  }

  async descartar(operationId: string) {
    await this.db.run("DELETE FROM outbox WHERE operation_id = ? AND estado = 'DEAD_LETTER'", [operationId]);
  }

  async listar() {
    return (await this.db.all<Linha>("SELECT * FROM outbox WHERE estado <> 'ENVIADO' ORDER BY criado_em DESC")).map(paraOperacao);
  }

  async contar() {
    const r = await this.db.first<{ pendentes: number; dead: number }>(
      "SELECT SUM(estado = 'PENDENTE') AS pendentes, SUM(estado = 'DEAD_LETTER') AS dead FROM outbox",
    );
    return { pendentes: r?.pendentes ?? 0, deadLetter: r?.dead ?? 0 };
  }

  async compactarEnviadasAntesDe(limite: Date) {
    return (await this.db.run("DELETE FROM outbox WHERE estado = 'ENVIADO' AND criado_em < ?", [limite.toISOString()])).changes;
  }
}
