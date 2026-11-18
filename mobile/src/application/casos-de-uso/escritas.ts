// Escritas: aplicam no aparelho na hora (a tela muda sem esperar a rede), entram na outbox na mesma transação e
// tentam sair já. O cadastro de morador é a exceção: exige conexão, para CPF e CNS inteiros nunca ficarem gravados.

import type { AcaoDoCuidador } from '@/domain/alerta/alerta-de-calor';
import type { Clock } from '@/domain/compartilhado/clock';
import { falha, ok, type Result } from '@/domain/compartilhado/result';
import { CartaoSus } from '@/domain/morador/cartao-sus';
import { Cpf } from '@/domain/morador/cpf';
import { Telefone } from '@/domain/morador/telefone';

import type { Barramento } from '../barramento';
import type { Morador, NovoMorador, ResultadoDoEnvio, SituacaoDoCheckIn } from '../modelos';
import type { CoreGateway } from '../ports/core-gateway';
import type { GeradorDeId, Operacao, OutboxRepository, PayloadDe, TipoDeOperacao } from '../ports/outbox';
import type { AlertaRepository, CasaRepository, MoradorRepository } from '../ports/repositorios';
import type { UnidadeDeTrabalho } from '../ports/servicos';
import { type Afetados, aplicarOperacao } from '../sincronizacao/aplicar';
import type { MotorDeSincronizacao } from '../sincronizacao/motor';

export type DepsDasEscritas = {
  gateway: CoreGateway;
  casas: CasaRepository;
  alertas: AlertaRepository;
  moradores: MoradorRepository;
  outbox: OutboxRepository;
  motor: MotorDeSincronizacao;
  unidade: UnidadeDeTrabalho;
  ids: GeradorDeId;
  clock: Clock;
  barramento: Barramento;
};

export class Escritas {
  constructor(private readonly d: DepsDasEscritas) {}

  private async enfileirar<T extends TipoDeOperacao>(tipo: T, payload: PayloadDe[T]): Promise<ResultadoDoEnvio> {
    const op = { operationId: this.d.ids.novo(), tipo, payload, criadoEm: this.d.clock.now().toISOString() } as Operacao;
    const afetados: Afetados = await this.d.unidade.executar(async (r) => {
      await r.outbox.enfileirar(op);
      return aplicarOperacao(op, r);
    });
    this.d.barramento.publicar(
      { recurso: 'casas' },
      { recurso: 'alertas' },
      { recurso: 'sincronizacao' },
      ...(afetados.domicilioId ? [{ recurso: 'casa' as const, id: afetados.domicilioId }] : []),
      ...(afetados.alertaId ? [{ recurso: 'alerta' as const, id: afetados.alertaId }] : []),
    );
    await this.d.motor.drenar();
    const depois = (await this.d.outbox.listar()).find((o) => o.operationId === op.operationId);
    if (!depois) return 'ENVIADO';
    return depois.estado === 'DEAD_LETTER' ? 'RECUSADO' : 'NA_FILA';
  }

  registrarCheckIn(domicilioId: string, situacao: SituacaoDoCheckIn) {
    return this.enfileirar('CHECK_IN', { domicilioId, situacao, momento: this.d.clock.now().toISOString() });
  }

  reconhecerAlerta(alertaId: string, acao: AcaoDoCuidador) {
    return this.enfileirar('RECONHECER_ALERTA', { alertaId, acao, momento: this.d.clock.now().toISOString() });
  }

  // Os value objects validam antes de qualquer rede; campo vazio na edição quer dizer "manter".
  async salvarMorador(domicilioId: string, moradorId: string | undefined, dados: NovoMorador): Promise<Result<Morador>> {
    const editando = moradorId !== undefined;
    const envio: Partial<NovoMorador> = { nome: dados.nome.trim(), idade: dados.idade, fatoresDeRisco: dados.fatoresDeRisco };
    if (!envio.nome) return falha('NOME', 'Escreva o nome.');
    if (!Number.isInteger(dados.idade) || dados.idade < 0 || dados.idade > 130) return falha('IDADE', 'Confira a idade.');
    for (const [campo, texto, criar] of [
      ['cpf', dados.cpf, Cpf.criar],
      ['cartaoSus', dados.cartaoSus, CartaoSus.criar],
      ['telefone', dados.telefone, Telefone.criar],
    ] as const) {
      if (!texto && (editando || campo !== 'cpf')) continue;
      const r = criar(texto);
      if (!r.ok) return r;
      envio[campo] = r.valor.paraEnvio();
    }
    if (dados.perfilDeSaude) envio.perfilDeSaude = dados.perfilDeSaude;

    const salvo = await this.d.gateway.salvarMorador(domicilioId, moradorId, envio);
    const outros = (await this.d.moradores.listarDaCasa(domicilioId)).filter((m) => m.id !== salvo.id);
    await this.d.moradores.substituirDaCasa(domicilioId, [...outros, salvo]);
    this.d.barramento.publicar({ recurso: 'casa', id: domicilioId });
    return ok(salvo);
  }
}
