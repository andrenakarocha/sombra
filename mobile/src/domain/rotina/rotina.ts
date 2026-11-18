// Rotina da casa (spec 01: R11 e R12; D13): check-in diário opcional no horário que a pessoa escolhe e lembrete de água
// no pico da tarde. Nenhum lembrete entre 22h e 7h; o de água só em dia de calor (≥ ATENCAO), no máximo 2 por dia.

import { falha, ok, type Result } from '../compartilhado/result';
import { gravidade, type NivelDeRisco } from '../clima/nivel-de-risco';

export type DadosDaRotina = {
  checkInDiario: { ativo: boolean; horario: string };
  lembreteDeAgua: { ativo: boolean };
};

export type Lembrete = { tipo: 'CHECK_IN_DIARIO' | 'AGUA'; horario: string; texto: string };

// Dentro da janela de 15h a 16h, com folga entre os dois.
export const HORARIOS_DA_AGUA = ['15:00', '15:45'] as const;

const minutos = (hhmm: string) => Number(hhmm.slice(0, 2)) * 60 + Number(hhmm.slice(3, 5));

export class Rotina {
  private constructor(readonly dados: Readonly<DadosDaRotina>) {}

  static padrao() {
    return new Rotina({ checkInDiario: { ativo: false, horario: '09:00' }, lembreteDeAgua: { ativo: true } });
  }

  static criar(dados: DadosDaRotina): Result<Rotina> {
    const { horario } = dados.checkInDiario;
    if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(horario)) return falha('HORARIO', 'O horário é no formato 09:00.');
    const m = minutos(horario);
    if (dados.checkInDiario.ativo && (m < 7 * 60 || m >= 22 * 60)) {
      return falha('R12', 'Entre 22h e 7h o Sombra não manda lembrete. Escolha um horário de dia.');
    }
    return ok(new Rotina({ checkInDiario: { ...dados.checkInDiario }, lembreteDeAgua: { ...dados.lembreteDeAgua } }));
  }

  // O que avisar hoje, em ordem, dado o nível de calor previsto para o dia.
  lembretesDoDia(nivelDoDia: NivelDeRisco): Lembrete[] {
    const lista: Lembrete[] = [];
    if (this.dados.checkInDiario.ativo) {
      lista.push({
        tipo: 'CHECK_IN_DIARIO',
        horario: this.dados.checkInDiario.horario,
        texto: 'Bom dia! Aperte ESTOU BEM para sua família saber que está tudo certo. Se esquecer, ela só recebe um lembrete.',
      });
    }
    if (this.dados.lembreteDeAgua.ativo && gravidade(nivelDoDia) >= gravidade('ATENCAO')) {
      for (const horario of HORARIOS_DA_AGUA) {
        lista.push({ tipo: 'AGUA', horario, texto: 'Hora mais quente do dia: beba um copo de água e fique na sombra.' });
      }
    }
    return lista.sort((a, b) => minutos(a.horario) - minutos(b.horario));
  }
}
