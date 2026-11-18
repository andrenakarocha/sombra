import { View } from 'react-native';
import { useStore } from 'zustand';

import { reproducaoStore } from '@/application/estado/reproducao';
import { INFO_DO_NIVEL } from '@/domain/clima/nivel-de-risco';

import { espaco, raio, useTema } from '../tema';
import { formatarGraus } from '../util/formato';

import { Texto } from './Texto';

const dataEHora = new Intl.DateTimeFormat('pt-BR', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  timeZone: 'America/Sao_Paulo',
});

// Deixa claro, a cada hora, que o número é real e de onde vem; e a diferença entre a régua padrão e a da pessoa.
export function AvisoDoReplay() {
  const tema = useTema();
  const r = useStore(reproducaoStore);
  if (!r.ativo || !r.momentoOriginal) return null;
  return (
    <View
      accessibilityRole="summary"
      accessibilityLiveRegion="polite"
      style={{ backgroundColor: tema.superficieAlternativa, borderRadius: raio.medio, padding: espaco[3], gap: espaco[1] }}
    >
      <Texto forte>
        Dados reais do INMET · {dataEHora.format(new Date(r.momentoOriginal))}h ({r.passo}/{r.total})
      </Texto>
      <Texto variante="pequeno">
        {formatarGraus(r.temperaturaC!)} °C · índice de calor {formatarGraus(r.indiceC!)} °C · {r.fonte}
      </Texto>
      <Texto variante="pequeno">
        Régua padrão (NWS): {INFO_DO_NIVEL[r.nivelPadrao!].rotulo} · para quem mora na casa:{' '}
        {INFO_DO_NIVEL[r.nivelPersonalizado!].rotulo}
      </Texto>
    </View>
  );
}
