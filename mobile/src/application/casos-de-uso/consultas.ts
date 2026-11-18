// Consultas local-first: pede ao Core, grava no aparelho, reaplica o que ainda está na fila e responde sempre do
// banco local. Sem rede (ou com o Core fora do ar), responde do banco com `origem: 'local'`.

import type { Clock } from '@/domain/compartilhado/clock';
import { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';

import { foraDoAr } from '../erros';
import { alertaDoServidor, alertaParaTela } from '../mapeadores';
import type { Alerta, Domicilio, PainelDaCasa, PontoDeResfriamento, PrivacidadeDaCasa, Resultado } from '../modelos';
import type { CoreGateway } from '../ports/core-gateway';
import type { OutboxRepository } from '../ports/outbox';
import type {
  AlertaRepository,
  CasaRepository,
  LeituraRepository,
  MoradorRepository,
  RedeRepository,
  SincronizacaoRepository,
} from '../ports/repositorios';
import type { RepositoriosDaUnidade, UnidadeDeTrabalho } from '../ports/servicos';
import { aplicarOperacao } from '../sincronizacao/aplicar';

const DIA_MS = 24 * 60 * 60 * 1000;

export class CasaNaoEncontrada extends Error {
  constructor(readonly id: string) {
    super('Esta casa não está mais na sua rede de cuidado.');
  }
}

export type DepsDasConsultas = {
  gateway: CoreGateway;
  casas: CasaRepository;
  leituras: LeituraRepository;
  alertas: AlertaRepository;
  moradores: MoradorRepository;
  rede: RedeRepository;
  sincronizacao: SincronizacaoRepository;
  outbox: OutboxRepository;
  unidade: UnidadeDeTrabalho;
  clock: Clock;
};

export class Consultas {
  constructor(private readonly d: DepsDasConsultas) {}

  // Busca no servidor e grava; só "fora do ar" cai no local, e só se já houve uma sincronização antes.
  private async atualizar<T>(
    recurso: string,
    buscar: () => Promise<T>,
    gravar: (dados: T, r: RepositoriosDaUnidade) => Promise<void>,
  ) {
    let dados: T;
    try {
      dados = await buscar();
    } catch (erro) {
      if (!foraDoAr(erro) || !(await this.d.sincronizacao.quando(recurso))) throw erro;
      return 'local' as const;
    }
    await this.d.unidade.executar(async (r) => {
      await gravar(dados, r);
      // Rebase: o que ainda está na fila é reaplicado por cima do retrato do servidor, na mesma transação.
      for (const op of await r.outbox.listar()) if (op.estado === 'PENDENTE') await aplicarOperacao(op, r);
      await r.sincronizacao.marcar(recurso, this.d.clock.now());
    });
    return 'servidor' as const;
  }

  async casas(): Promise<Resultado<Domicilio[]>> {
    const origem = await this.atualizar('casas', () => this.d.gateway.listarCasas(), (c, r) => r.casas.substituirTodas(c));
    return { dados: await this.d.casas.listar(), atualizadoEm: await this.d.sincronizacao.quando('casas'), origem };
  }

  // Tudo o que a tela da casa (e o Modo Idoso) mostra, numa consulta só.
  async painelDaCasa(id: string): Promise<Resultado<PainelDaCasa>> {
    const agora = this.d.clock.now();
    const desde = new Date(agora.getTime() - DIA_MS);
    const recurso = `casa:${id}`;
    const origem = await this.atualizar(
      recurso,
      async () => {
        const [casa, leituras, moradores, rede] = await Promise.all([
          this.d.gateway.buscarCasa(id),
          this.d.gateway.leituras(id, desde, agora),
          this.d.gateway.moradores(id),
          this.d.gateway.redeDeCuidado(id),
        ]);
        // O alerta aberto vem inteiro: sem ele, um "estou bem" offline não teria o que resolver no aparelho.
        const alerta = casa.alertaAberto ? await this.d.gateway.buscarAlerta(casa.alertaAberto.id) : null;
        return { casa, leituras, moradores, rede, alerta };
      },
      async ({ casa, leituras, moradores, rede, alerta }, r) => {
        await r.casas.salvar(casa);
        await r.leituras.inserir(id, leituras);
        await r.moradores.substituirDaCasa(id, moradores);
        await r.rede.substituirDaCasa(id, rede);
        if (alerta) {
          const salvo = alertaDoServidor(alerta);
          await r.alertas.salvar(salvo.alerta, salvo.apelidoDaCasa);
        }
      },
    );
    const casa = await this.d.casas.buscar(id);
    if (!casa) throw new CasaNaoEncontrada(id);
    const moradores = await this.d.moradores.listarDaCasa(id);
    const primeiro = moradores[0];
    return {
      dados: {
        casa,
        leituras24h: await this.d.leituras.desde(id, desde),
        moradores,
        rede: await this.d.rede.listarDaCasa(id),
        risco: primeiro ? RiscoPersonalizado.para(primeiro.fatoresDeRisco, primeiro.idade) : null,
      },
      atualizadoEm: await this.d.sincronizacao.quando(recurso),
      origem,
    };
  }

  async alertas(): Promise<Resultado<Alerta[]>> {
    const origem = await this.atualizar(
      'alertas',
      () => this.d.gateway.listarAlertas(),
      (lista, r) => r.alertas.substituirTodos(lista.map(alertaDoServidor)),
    );
    const salvos = await this.d.alertas.listar();
    return {
      dados: salvos.map((s) => alertaParaTela(s, this.d.clock)),
      atualizadoEm: await this.d.sincronizacao.quando('alertas'),
      origem,
    };
  }

  async alerta(id: string): Promise<Resultado<Alerta>> {
    const origem = await this.atualizar(
      `alerta:${id}`,
      () => this.d.gateway.buscarAlerta(id),
      async (dto, r) => {
        const salvo = alertaDoServidor(dto);
        await r.alertas.salvar(salvo.alerta, salvo.apelidoDaCasa);
      },
    );
    const salvo = await this.d.alertas.buscar(id);
    if (!salvo) throw new CasaNaoEncontrada(id);
    return { dados: alertaParaTela(salvo, this.d.clock), atualizadoEm: await this.d.sincronizacao.quando(`alerta:${id}`), origem };
  }

  // Estes dois vêm sempre do servidor: o mapa de pontos muda e o farol de privacidade precisa ser o de agora.
  pontosDeResfriamento(id: string): Promise<PontoDeResfriamento[]> {
    return this.d.gateway.pontosDeResfriamento(id);
  }

  privacidade(id: string): Promise<PrivacidadeDaCasa> {
    return this.d.gateway.privacidade(id);
  }
}
