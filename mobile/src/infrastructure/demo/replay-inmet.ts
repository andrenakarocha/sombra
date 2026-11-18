// Replay da onda de calor real de novembro de 2023 (INMET, A701), extraída por mobile/scripts/extrair_replay_inmet.py.
// Cada hora passa pelos value objects e pelo índice de calor do domínio; o nível da casa é o da régua personalizada
// do morador (R3), como o Core classifica. Os dados são da estação a céu aberto: nenhum número é ajustado.

import type { Leitura, Morador } from '@/application/modelos';
import { IndiceDeCalor } from '@/domain/clima/indice-de-calor';
import type { NivelDeRisco } from '@/domain/clima/nivel-de-risco';
import { Temperatura } from '@/domain/clima/temperatura';
import { UmidadeRelativa } from '@/domain/clima/umidade-relativa';
import { valorOuErro } from '@/domain/compartilhado/result';
import { RiscoPersonalizado } from '@/domain/risco/risco-personalizado';

import dados from './replay-a701-2023-11.json';

export const REPLAY = dados;

export type HoraDoReplay = (typeof dados.horas)[number];

export type Passo = { leitura: Leitura; nivelPadrao: NivelDeRisco; nivelPersonalizado: NivelDeRisco };

// `momento` é a hora no relógio da demonstração (a leitura é real; o horário é deslocado para "agora").
export function passoDoReplay(hora: HoraDoReplay, morador: Pick<Morador, 'fatoresDeRisco' | 'idade'> | undefined, momento: Date): Passo {
  const indice = IndiceDeCalor.calcular(valorOuErro(Temperatura.emCelsius(hora.temperaturaC)), valorOuErro(UmidadeRelativa.de(hora.umidadePct)));
  const risco = RiscoPersonalizado.para(morador?.fatoresDeRisco ?? [], morador?.idade ?? 0);
  return {
    leitura: {
      momento: momento.toISOString(),
      temperaturaC: hora.temperaturaC,
      umidadePct: hora.umidadePct,
      indiceDeCalorC: Math.round(indice.celsius * 10) / 10,
    },
    nivelPadrao: indice.nivel,
    nivelPersonalizado: risco.avaliar(indice.celsius).personalizado,
  };
}
