// Centro de Privacidade do aparelho (LGPD art. 18: acesso, portabilidade e eliminação). Exporta o que este celular
// guarda, sem PII inteira (CPF e CNS já chegam mascarados; os telefones saem mascarados aqui), e apaga tudo.

import type { Clock } from '@/domain/compartilhado/clock';
import { Telefone } from '@/domain/morador/telefone';

import type { OutboxRepository } from '../ports/outbox';
import type { AlertaRepository, CasaRepository, MoradorRepository, RedeRepository } from '../ports/repositorios';

const mascararTelefone = (t: string | null) => {
  if (!t) return null;
  const r = Telefone.criar(t);
  return r.ok ? r.valor.mascarado : '[TELEFONE]';
};

export class PrivacidadeLocal {
  constructor(
    private readonly d: {
      casas: CasaRepository;
      alertas: AlertaRepository;
      moradores: MoradorRepository;
      rede: RedeRepository;
      outbox: OutboxRepository;
      apagarTudo: () => Promise<void>;
      clock: Clock;
    },
  ) {}

  async exportar(): Promise<string> {
    const casas = await this.d.casas.listar();
    const porCasa = await Promise.all(
      casas.map(async (c) => ({
        casa: { id: c.id, apelido: c.apelido, bairro: c.bairro, nivelAtual: c.nivelAtual, ultimoCheckIn: c.ultimoCheckIn },
        moradores: (await this.d.moradores.listarDaCasa(c.id)).map((m) => ({ ...m, telefone: mascararTelefone(m.telefone) })),
        redeDeCuidado: (await this.d.rede.listarDaCasa(c.id)).map((r) => ({ ...r, telefone: mascararTelefone(r.telefone) })),
      })),
    );
    const documento = {
      sobre: 'Dados que o aplicativo Sombra guarda neste celular (LGPD, art. 18). Os dados completos ficam no Core.',
      geradoEm: this.d.clock.now().toISOString(),
      casas: porCasa,
      alertas: (await this.d.alertas.listar()).map(({ alerta, apelidoDaCasa }) => ({ ...alerta.dados, apelidoDaCasa })),
      // Só o tipo e o estado: o conteúdo das operações já está nas casas e nos alertas acima.
      filaDeEnvio: (await this.d.outbox.listar()).map((o) => ({ tipo: o.tipo, estado: o.estado, criadoEm: o.criadoEm })),
    };
    return JSON.stringify(documento, null, 2);
  }

  apagarTudo() {
    return this.d.apagarTudo();
  }
}
