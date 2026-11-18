// Morador e rede de cuidado: dados da casa que o servidor manda inteiros e o app só substitui.

import type { ContatoDaRede, Morador } from '@/application/modelos';
import type { MoradorRepository, RedeRepository } from '@/application/ports/repositorios';

import type { SqlDatabase } from '../sql-database';

type LinhaMorador = {
  id: string;
  nome: string;
  idade: number;
  telefone: string | null;
  cpf_mascarado: string;
  cartao_sus_mascarado: string;
  perfil_de_saude: string;
  fatores_de_risco: string;
};

export class MoradorRepositorySqlite implements MoradorRepository {
  constructor(private readonly db: SqlDatabase) {}

  // Só a forma mascarada chega ao banco local: o CPF e o CNS inteiros nunca ficam no aparelho.
  substituirDaCasa(domicilioId: string, moradores: Morador[]) {
    return this.db.transaction(async (tx) => {
      await tx.run('DELETE FROM moradores WHERE domicilio_id = ?', [domicilioId]);
      for (const m of moradores) {
        await tx.run(
          `INSERT INTO moradores (id, domicilio_id, nome, idade, telefone, cpf_mascarado, cartao_sus_mascarado,
                                  perfil_de_saude, fatores_de_risco)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
          [m.id, domicilioId, m.nome, m.idade, m.telefone, m.cpf, m.cartaoSus, m.perfilDeSaude, JSON.stringify(m.fatoresDeRisco)],
        );
      }
    });
  }

  async listarDaCasa(domicilioId: string): Promise<Morador[]> {
    const linhas = await this.db.all<LinhaMorador>('SELECT * FROM moradores WHERE domicilio_id = ? ORDER BY nome', [domicilioId]);
    return linhas.map((l) => ({
      id: l.id,
      nome: l.nome,
      idade: l.idade,
      telefone: l.telefone,
      cpf: l.cpf_mascarado,
      cartaoSus: l.cartao_sus_mascarado,
      perfilDeSaude: l.perfil_de_saude,
      fatoresDeRisco: JSON.parse(l.fatores_de_risco),
    }));
  }
}

export class RedeRepositorySqlite implements RedeRepository {
  constructor(private readonly db: SqlDatabase) {}

  substituirDaCasa(domicilioId: string, contatos: ContatoDaRede[]) {
    return this.db.transaction(async (tx) => {
      await tx.run('DELETE FROM rede_de_cuidado WHERE domicilio_id = ?', [domicilioId]);
      for (const [ordem, c] of contatos.entries()) {
        await tx.run(
          'INSERT INTO rede_de_cuidado (domicilio_id, ordem, id, nome, papel, telefone) VALUES (?, ?, ?, ?, ?, ?)',
          [domicilioId, ordem, c.id, c.nome, c.papel, c.telefone],
        );
      }
    });
  }

  async listarDaCasa(domicilioId: string): Promise<ContatoDaRede[]> {
    return this.db.all<ContatoDaRede>(
      'SELECT id, nome, papel, telefone FROM rede_de_cuidado WHERE domicilio_id = ? ORDER BY ordem',
      [domicilioId],
    );
  }
}
