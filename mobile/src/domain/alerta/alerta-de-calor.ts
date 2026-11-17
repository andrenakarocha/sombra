// Máquina de estados do alerta de calor, igual às regras do Core (spec 01: R4 a R8; D13).
// Imutável: toda transição devolve um alerta novo, ou uma falha com o código da regra.

import { type Clock } from '../compartilhado/clock';
import { falha, ok, type Result } from '../compartilhado/result';
import { gravidade, type NivelDeRisco } from '../clima/nivel-de-risco';

export type EstadoDoAlerta = 'AGUARDANDO_CHECKIN' | 'ESCALONADO' | 'RECONHECIDO' | 'RESOLVIDO';
export type TipoDeAlerta = 'PREVENTIVO' | 'DE_SENTINELA';
export type Destinatario = 'FAMILIAR' | 'VIZINHO' | 'REDE';
export type ResolvidoPor = 'CHECKIN_SENTINELA' | 'CHECKIN_APP' | 'CUIDADOR';
export type AcaoDoCuidador = 'VOU_LIGAR' | 'ESTOU_INDO' | 'ELA_ESTA_BEM';

export type EventoDoAlerta = { momento: string; degrau: number; descricao: string };

type Dados = {
  id: string;
  domicilioId: string;
  tipo: TipoDeAlerta;
  estado: EstadoDoAlerta;
  nivel: NivelDeRisco;
  degrau: number;
  abertoEm: string;
  prazo: string;
  pedidoDeAjuda: boolean;
  temVizinho: boolean;
  resolvidoPor: ResolvidoPor | null;
  linhaDoTempo: EventoDoAlerta[];
};

const MINUTO = 60_000;

// R6: prazo do "estou bem" por nível; depois do primeiro degrau, o próximo vem a cada 10 min.
export const PRAZO_MIN: Partial<Record<NivelDeRisco, number>> = { CAUTELA_EXTREMA: 30, PERIGO: 15, PERIGO_EXTREMO: 15 };
export const INTERVALO_ENTRE_DEGRAUS_MIN = 10;

const DESCRICAO_DO_DEGRAU: Record<number, string> = {
  1: 'Lembrete leve para o primeiro familiar.',
  2: '"Quem está perto": pedido para a vizinha dar uma olhada.',
  3: 'Alerta para toda a rede de cuidado.',
};

const mais = (iso: string | Date, minutos: number) => new Date(new Date(iso).getTime() + minutos * MINUTO).toISOString();

export class AlertaDeCalor {
  private constructor(private readonly d: Readonly<Dados>) {}

  static abrir(params: {
    id: string;
    domicilioId: string;
    tipo: TipoDeAlerta;
    nivel: NivelDeRisco;
    temVizinho: boolean;
    clock: Clock;
  }): Result<AlertaDeCalor> {
    const prazoMin = PRAZO_MIN[params.nivel];
    if (params.tipo === 'DE_SENTINELA' && prazoMin === undefined) {
      return falha('R6', 'O "estou bem" só é pedido a partir de CAUTELA_EXTREMA.');
    }
    const agora = params.clock.now().toISOString();
    return ok(
      new AlertaDeCalor({
        id: params.id,
        domicilioId: params.domicilioId,
        tipo: params.tipo,
        estado: 'AGUARDANDO_CHECKIN',
        nivel: params.nivel,
        degrau: 0,
        abertoEm: agora,
        // O preventivo (previsão de 24 h) não pede "estou bem" agora: o prazo é só informativo.
        prazo: mais(agora, prazoMin ?? 24 * 60),
        pedidoDeAjuda: false,
        temVizinho: params.temVizinho,
        resolvidoPor: null,
        linhaDoTempo: [
          {
            momento: agora,
            degrau: 0,
            descricao:
              params.tipo === 'PREVENTIVO'
                ? 'Previsão de calor perigoso nas próximas 24 horas.'
                : 'A Sentinela lembrou e pediu o "estou bem".',
          },
        ],
      }),
    );
  }

  static restaurar(dados: Dados) {
    return new AlertaDeCalor({ ...dados, linhaDoTempo: [...dados.linhaDoTempo] });
  }

  get dados(): Readonly<Dados> {
    return this.d;
  }

  get resolvido() {
    return this.d.estado === 'RESOLVIDO';
  }

  // Degraus 2 e 3 só com perigo de verdade ou pedido de ajuda (R8): esquecer num dia sem risco para no 1.
  get degrauMaximo() {
    return this.d.pedidoDeAjuda || gravidade(this.d.nivel) >= gravidade('PERIGO') ? 3 : 1;
  }

  private com(mudanca: Partial<Dados>, descricao: string, momento: Date): AlertaDeCalor {
    const degrau = mudanca.degrau ?? this.d.degrau;
    return new AlertaDeCalor({
      ...this.d,
      ...mudanca,
      linhaDoTempo: [...this.d.linhaDoTempo, { momento: momento.toISOString(), degrau, descricao }],
    });
  }

  // R4: uma leitura nova atualiza o alerta aberto em vez de abrir outro. Subir de nível encurta o prazo.
  atualizarNivel(nivel: NivelDeRisco, clock: Clock): Result<AlertaDeCalor> {
    if (this.resolvido) return falha('R5', 'Um alerta resolvido não recebe leituras.');
    if (gravidade(nivel) <= gravidade(this.d.nivel)) return ok(this);
    const agora = clock.now();
    const prazoNovo = mais(agora, PRAZO_MIN[nivel] ?? 15);
    const prazo = prazoNovo < this.d.prazo ? prazoNovo : this.d.prazo;
    return ok(this.com({ nivel, prazo }, `O calor subiu para ${nivel}.`, agora));
  }

  // R8: o próximo degrau só depois do prazo, nunca depois de alguém reconhecer, e pula o 2 se não há vizinho.
  escalonar(clock: Clock): Result<{ alerta: AlertaDeCalor; destinatario: Destinatario }> {
    const agora = clock.now();
    if (this.resolvido) return falha('R5', 'Um alerta resolvido não pode ser escalonado.');
    if (this.d.estado === 'RECONHECIDO') return falha('R8', 'Alguém da rede já está cuidando: o escalonamento parou.');
    if (agora.toISOString() < this.d.prazo) return falha('R8', 'O prazo do degrau atual ainda não venceu.');
    if (this.d.degrau >= this.degrauMaximo) {
      return falha('R8', this.degrauMaximo === 1 ? 'Sem perigo, o escalonamento para no lembrete leve.' : 'A rede inteira já foi avisada.');
    }
    const degrau = this.d.degrau === 1 && !this.d.temVizinho ? 3 : this.d.degrau + 1;
    const destinatario: Destinatario = degrau === 1 ? 'FAMILIAR' : degrau === 2 ? 'VIZINHO' : 'REDE';
    const alerta = this.com(
      { estado: 'ESCALONADO', degrau, prazo: mais(agora, INTERVALO_ENTRE_DEGRAUS_MIN) },
      DESCRICAO_DO_DEGRAU[degrau]!,
      agora,
    );
    return ok({ alerta, destinatario });
  }

  // "Estou bem" resolve; "preciso de ajuda" vai direto ao degrau 3, em qualquer nível (R7).
  checkIn(situacao: 'ESTOU_BEM' | 'PRECISO_DE_AJUDA', origem: 'APP' | 'SENTINELA', clock: Clock): Result<AlertaDeCalor> {
    if (this.resolvido) return falha('R5', 'Este alerta já foi resolvido.');
    const agora = clock.now();
    if (situacao === 'PRECISO_DE_AJUDA') {
      return ok(
        this.com({ estado: 'ESCALONADO', degrau: 3, pedidoDeAjuda: true }, 'Ela pediu ajuda: toda a rede foi avisada.', agora),
      );
    }
    return ok(
      this.com(
        { estado: 'RESOLVIDO', resolvidoPor: origem === 'APP' ? 'CHECKIN_APP' : 'CHECKIN_SENTINELA' },
        origem === 'APP' ? 'Ela apertou "estou bem" no celular.' : 'Ela apertou o botão da Sentinela.',
        agora,
      ),
    );
  }

  reconhecer(acao: AcaoDoCuidador, clock: Clock): Result<AlertaDeCalor> {
    if (this.resolvido) return falha('R5', 'Este alerta já foi resolvido.');
    const agora = clock.now();
    if (acao === 'ELA_ESTA_BEM') {
      return ok(this.com({ estado: 'RESOLVIDO', resolvidoPor: 'CUIDADOR' }, 'A família confirmou que ela está bem.', agora));
    }
    const descricao = acao === 'VOU_LIGAR' ? 'Alguém da família vai ligar.' : 'Alguém da família está indo até a casa.';
    return ok(this.com({ estado: 'RECONHECIDO' }, descricao, agora));
  }

  minutosAteResolver(): number | null {
    if (!this.resolvido) return null;
    const fim = this.d.linhaDoTempo.at(-1)!.momento;
    return Math.round((new Date(fim).getTime() - new Date(this.d.abertoEm).getTime()) / MINUTO);
  }

  // Para a tela: quanto falta para o próximo degrau, ou null se ele não vai acontecer.
  minutosAteProximoDegrau(clock: Clock): number | null {
    if (this.resolvido || this.d.estado === 'RECONHECIDO' || this.d.degrau >= this.degrauMaximo) return null;
    return Math.max(0, Math.ceil((new Date(this.d.prazo).getTime() - clock.now().getTime()) / MINUTO));
  }
}
