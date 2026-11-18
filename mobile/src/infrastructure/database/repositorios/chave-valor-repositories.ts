import type { PreferenciaRepository, SincronizacaoRepository } from '@/application/ports/repositorios';

import type { SqlDatabase } from '../sql-database';

export class PreferenciaRepositorySqlite implements PreferenciaRepository {
  constructor(private readonly db: SqlDatabase) {}

  async ler(chave: string) {
    return (await this.db.first<{ valor: string }>('SELECT valor FROM preferencias WHERE chave = ?', [chave]))?.valor ?? null;
  }

  async gravar(chave: string, valor: string | null) {
    if (valor === null) await this.db.run('DELETE FROM preferencias WHERE chave = ?', [chave]);
    else await this.db.run('INSERT OR REPLACE INTO preferencias (chave, valor) VALUES (?, ?)', [chave, valor]);
  }
}

export class SincronizacaoRepositorySqlite implements SincronizacaoRepository {
  constructor(private readonly db: SqlDatabase) {}

  async marcar(recurso: string, momento: Date) {
    await this.db.run('INSERT OR REPLACE INTO sincronizacao (recurso, atualizado_em) VALUES (?, ?)', [recurso, momento.toISOString()]);
  }

  async quando(recurso: string) {
    return (await this.db.first<{ atualizado_em: string }>('SELECT atualizado_em FROM sincronizacao WHERE recurso = ?', [recurso]))
      ?.atualizado_em ?? null;
  }
}

// Sair da conta ou "apagar tudo" no Centro de Privacidade: nenhum dado de casa fica no aparelho.
export async function apagarDadosLocais(db: SqlDatabase, { inclusivePreferencias }: { inclusivePreferencias: boolean }) {
  await db.transaction(async (tx) => {
    await tx.exec(`DELETE FROM eventos_do_alerta; DELETE FROM alertas; DELETE FROM leituras; DELETE FROM moradores;
                   DELETE FROM rede_de_cuidado; DELETE FROM casas; DELETE FROM outbox; DELETE FROM sincronizacao;`);
    if (inclusivePreferencias) await tx.exec('DELETE FROM preferencias');
  });
}
