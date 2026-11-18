// O Core da demonstração: implementa a mesma porta do HTTP, em memória, com a mesma máquina de estados do domínio
// para os alertas. A simulação emite eventos como o hub emitiria, e o app os recebe pelo mesmo caminho do SignalR.
// Assim o professor abre o app sem subir o backend (spec 05, "Modo demonstração").

import { ErroDaApi } from '@/application/erros';
import type { Alerta, Domicilio, Leitura, Morador, NovoMorador } from '@/application/modelos';
import type { CoreGateway } from '@/application/ports/core-gateway';
import type { Operacao } from '@/application/ports/outbox';
import type { CanalDeTempoReal } from '@/application/ports/servicos';
import { AlertaDeCalor } from '@/domain/alerta/alerta-de-calor';
import { type Clock, FixedClock } from '@/domain/compartilhado/clock';
import { valorOuErro } from '@/domain/compartilhado/result';

import { ALERTAS, CASAS, leituras24h, MORADORES, PONTOS, PRIVACIDADE, REDE } from './fixtures';

const MINUTO = 60_000;
const copia = <T>(v: T): T => JSON.parse(JSON.stringify(v));
type Ouvinte = (evento: string, payload: Record<string, unknown>) => void;

type Estado = {
  casas: Domicilio[];
  alertas: Map<string, { alerta: AlertaDeCalor; apelido: string }>;
  leituras: Record<string, Leitura[]>;
  moradores: Record<string, Morador[]>;
};

export class CoreDemoGateway implements CoreGateway {
  private estado!: Estado;
  private readonly ouvintes = new Set<Ouvinte>();
  // Operações já aplicadas: o mesmo operationId não vale duas vezes (o que o Idempotency-Key garante no Core).
  private readonly aplicadas = new Set<string>();

  constructor(
    private readonly clock: Clock,
    private readonly latenciaMs = 150,
  ) {
    this.recomecar();
  }

  recomecar() {
    const agora = this.clock.now();
    const em = (min: number) => new Date(agora.getTime() + min * MINUTO).toISOString();
    const casas = copia(CASAS).map((c) => ({
      ...c,
      ultimaLeitura: c.ultimaLeitura && { ...c.ultimaLeitura, momento: agora.toISOString() },
      ultimoCheckIn: em(-6 * 60),
    }));
    const alertas = new Map<string, { alerta: AlertaDeCalor; apelido: string }>();
    for (const a of ALERTAS) {
      // Os alertas de exemplo nascem pela máquina de estados, num relógio no passado.
      const relogio = FixedClock.at(em(a.estado === 'RESOLVIDO' ? -23 * 60 : -10));
      let alerta = valorOuErro(
        AlertaDeCalor.abrir({ id: a.id, domicilioId: a.domicilioId, tipo: a.tipo, nivel: a.nivel, temVizinho: true, clock: relogio }),
      );
      if (a.estado === 'RESOLVIDO') {
        relogio.avancarMinutos(20);
        alerta = valorOuErro(alerta.checkIn('ESTOU_BEM', 'SENTINELA', relogio));
      }
      alertas.set(a.id, { alerta, apelido: a.apelidoDaCasa });
    }
    for (const c of casas) this.resumirAlerta(c, alertas);
    this.estado = {
      casas,
      alertas,
      leituras: Object.fromEntries(casas.map((c) => [c.id, leituras24h(agora, c.ultimaLeitura!)])),
      moradores: copia(MORADORES),
    };
    this.aplicadas.clear();
  }

  private resumirAlerta(casa: Domicilio, alertas = this.estado.alertas) {
    const aberto = [...alertas.values()].find((a) => a.alerta.dados.domicilioId === casa.id && !a.alerta.resolvido);
    const d = aberto?.alerta.dados;
    casa.alertaAberto = d ? { id: d.id, tipo: d.tipo, estado: d.estado, degrau: d.degrau, prazo: d.prazo } : null;
  }

  private async responder<T>(valor: () => T): Promise<T> {
    if (this.latenciaMs) await new Promise((r) => setTimeout(r, this.latenciaMs));
    return copia(valor());
  }

  private casa(id: string) {
    const c = this.estado.casas.find((x) => x.id === id);
    if (!c) throw new ErroDaApi(404, 'Casa não encontrada', 'demo0000');
    return c;
  }

  private paraDto(id: string): Alerta {
    const salvo = this.estado.alertas.get(id);
    if (!salvo) throw new ErroDaApi(404, 'Alerta não encontrado', 'demo0000');
    const d = salvo.alerta.dados;
    return { ...d, apelidoDaCasa: salvo.apelido, linhaDoTempo: [...d.linhaDoTempo] };
  }

  // ── CoreGateway ──────────────────────────────────────────────────────────────

  entrar() {
    return this.responder(() => ({ token: 'demo' }));
  }

  listarCasas() {
    return this.responder(() => this.estado.casas);
  }

  buscarCasa(id: string) {
    return this.responder(() => this.casa(id));
  }

  leituras(id: string) {
    return this.responder(() => [...(this.estado.leituras[id] ?? [])].reverse());
  }

  moradores(id: string) {
    return this.responder(() => this.estado.moradores[id] ?? []);
  }

  redeDeCuidado(id: string) {
    return this.responder(() => REDE[id] ?? []);
  }

  listarAlertas() {
    return this.responder(() => [...this.estado.alertas.keys()].map((id) => this.paraDto(id)));
  }

  buscarAlerta(id: string) {
    return this.responder(() => this.paraDto(id));
  }

  pontosDeResfriamento() {
    return this.responder(() => PONTOS);
  }

  privacidade(id: string) {
    return this.responder(() => PRIVACIDADE[id] ?? { farol: 'VERDE' as const, mensagens: [] });
  }

  salvarMorador(domicilioId: string, moradorId: string | undefined, dados: Partial<NovoMorador>) {
    return this.responder(() => {
      const lista = (this.estado.moradores[domicilioId] ??= []);
      const atual = lista.find((m) => m.id === moradorId);
      // Como o Core: a resposta volta mascarada (formato do contrato core-api.md).
      const salvo: Morador = {
        id: atual?.id ?? `m-${this.clock.now().getTime()}`,
        nome: dados.nome ?? atual?.nome ?? '',
        idade: dados.idade ?? atual?.idade ?? 0,
        cpf: dados.cpf ? `***.${dados.cpf.slice(3, 6)}.${dados.cpf.slice(6, 9)}-**` : (atual?.cpf ?? ''),
        cartaoSus: dados.cartaoSus ? `*** **** **** ${dados.cartaoSus.slice(-4)}` : (atual?.cartaoSus ?? ''),
        telefone: dados.telefone ?? atual?.telefone ?? null,
        perfilDeSaude: dados.perfilDeSaude ? '[DADO DE SAÚDE PROTEGIDO]' : (atual?.perfilDeSaude ?? ''),
        fatoresDeRisco: dados.fatoresDeRisco ?? atual?.fatoresDeRisco ?? [],
      };
      if (atual) Object.assign(atual, salvo);
      else lista.push(salvo);
      return salvo;
    });
  }

  async enviar(op: Operacao) {
    await this.responder(() => null);
    if (this.aplicadas.has(op.operationId)) return;
    this.aplicadas.add(op.operationId);
    const relogio = FixedClock.at(op.payload.momento);
    if (op.tipo === 'CHECK_IN') {
      const casa = this.casa(op.payload.domicilioId);
      casa.ultimoCheckIn = op.payload.momento;
      const aberto = casa.alertaAberto && this.estado.alertas.get(casa.alertaAberto.id);
      if (aberto) {
        const r = aberto.alerta.checkIn(op.payload.situacao, 'APP', relogio);
        if (r.ok) aberto.alerta = r.valor;
        this.resumirAlerta(casa);
      }
      this.emitir('CheckInRegistrado', { domicilioId: casa.id, momento: op.payload.momento, origem: 'APP', situacao: op.payload.situacao });
      return;
    }
    const salvo = this.estado.alertas.get(op.payload.alertaId);
    if (!salvo) throw new ErroDaApi(404, 'Alerta não encontrado', 'demo0000');
    const r = salvo.alerta.reconhecer(op.payload.acao, relogio);
    if (!r.ok) throw new ErroDaApi(422, r.erro.mensagem, 'demo0422');
    salvo.alerta = r.valor;
    this.resumirAlerta(this.casa(salvo.alerta.dados.domicilioId));
  }

  // ── Eventos (o "hub" da demonstração) ──────────────────────────────────────────

  private emitir(evento: string, payload: Record<string, unknown>) {
    for (const ouvinte of this.ouvintes) ouvinte(evento, payload);
  }

  readonly tempoReal: CanalDeTempoReal = {
    conectar: (_token, aoEvento) => {
      this.ouvintes.add(aoEvento);
      return () => this.ouvintes.delete(aoEvento);
    },
  };

  // A onda de calor do roteiro (§3.4 do plano): a primeira casa vai a PERIGO e a Sentinela pede o "estou bem".
  simularPerigo(leitura: Leitura, nivel: 'PERIGO' | 'PERIGO_EXTREMO' = 'PERIGO') {
    const casa = this.estado.casas[0]!;
    casa.nivelAtual = nivel;
    casa.ultimaLeitura = leitura;
    this.estado.leituras[casa.id] = [...(this.estado.leituras[casa.id] ?? []).slice(1), leitura];
    this.emitir('LeituraRegistrada', { domicilioId: casa.id, ...leitura, nivel });

    const aberto = casa.alertaAberto && this.estado.alertas.get(casa.alertaAberto.id);
    if (aberto) {
      aberto.alerta = valorOuErro(aberto.alerta.atualizarNivel(nivel, this.clock));
    } else {
      const alerta = valorOuErro(
        AlertaDeCalor.abrir({
          id: `demo-${this.clock.now().getTime()}`,
          domicilioId: casa.id,
          tipo: 'DE_SENTINELA',
          nivel,
          temVizinho: (REDE[casa.id] ?? []).some((c) => c.papel === 'VIZINHO'),
          clock: this.clock,
        }),
      );
      this.estado.alertas.set(alerta.dados.id, { alerta, apelido: casa.apelido });
    }
    this.resumirAlerta(casa);
    const d = this.estado.alertas.get(casa.alertaAberto!.id)!.alerta.dados;
    this.emitir('AlertaAberto', { alertaId: d.id, domicilioId: casa.id, tipo: d.tipo, nivel, prazo: d.prazo });
    return d.id;
  }

  // Sem resposta: sobe um degrau pela máquina de estados, mesmo antes do prazo (a demonstração não espera 15 min).
  escalonar(alertaId: string) {
    const salvo = this.estado.alertas.get(alertaId);
    if (!salvo || salvo.alerta.resolvido) return false;
    const adiantado = FixedClock.at(salvo.alerta.dados.prazo);
    const r = salvo.alerta.escalonar(adiantado);
    if (!r.ok) return false;
    salvo.alerta = r.valor.alerta;
    this.resumirAlerta(this.casa(salvo.alerta.dados.domicilioId));
    const mensagens = {
      FAMILIAR: `A ${salvo.apelido.replace('Casa da ', '')} ainda não confirmou que está bem. Quer ligar?`,
      VIZINHO: `Pode dar uma olhada na ${salvo.apelido.replace('Casa da ', '')}?`,
      REDE: `Alerta: ${salvo.apelido} sem resposta no calor perigoso.`,
    };
    this.emitir('AlertaEscalonado', {
      alertaId,
      domicilioId: salvo.alerta.dados.domicilioId,
      degrau: salvo.alerta.dados.degrau,
      destinatario: r.valor.destinatario,
      mensagem: mensagens[r.valor.destinatario],
    });
    return true;
  }
}
