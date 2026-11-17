import { View } from 'react-native';

import type { Domicilio } from '@/application/modelos';
import { espaco, fonte, raio, useTema } from '@/tema';
import { formatarGraus, formatarHora } from '@/util/formato';

import { Botao } from './Botao';
import { Farol } from './Farol';
import { Texto } from './Texto';

type Props = {
  casa: Domicilio;
  aoAbrir: () => void;
  aoLigar?: () => void;
};

export function CartaoCasa({ casa, aoAbrir, aoLigar }: Props) {
  const tema = useTema();
  const indice = casa.ultimaLeitura?.indiceDeCalorC;
  const online = casa.sentinela?.online ?? false;
  return (
    <View
      accessibilityLabel={casa.apelido}
      style={{
        backgroundColor: tema.superficie,
        borderColor: tema.borda,
        borderWidth: 1,
        borderRadius: raio.grande,
        padding: espaco[5],
        gap: espaco[4],
      }}
    >
      <View style={{ gap: espaco[2] }}>
        <Texto variante="subtitulo" accessibilityRole="header" style={{ fontFamily: fonte.display }}>
          {casa.apelido}
        </Texto>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          <View
            style={{
              width: 9,
              height: 9,
              borderRadius: 5,
              backgroundColor: online ? tema.risco.normal.borda : tema.textoSuave,
            }}
          />
          <Texto variante="pequeno" suave>
            {online ? 'Sentinela online' : 'Sentinela offline'} · {casa.bairro}
          </Texto>
        </View>
        <Farol nivel={casa.nivelAtual} />
      </View>

      {indice !== undefined && (
        <Texto
          variante="display"
          accessibilityLabel={`Índice de calor ${formatarGraus(indice)} graus`}
        >
          {formatarGraus(indice)}
          <Texto variante="subtitulo">°C</Texto>
        </Texto>
      )}

      <View
        style={{
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: espaco[4],
          paddingTop: espaco[4],
          borderTopWidth: 1,
          borderStyle: 'dashed',
          borderColor: tema.borda,
        }}
      >
        <Dado rotulo={'Último "estou bem"'} valor={casa.ultimoCheckIn ? formatarHora(casa.ultimoCheckIn) : '—'} />
        {casa.alertaAberto && <Dado rotulo="Responder até" valor={formatarHora(casa.alertaAberto.prazo)} />}
      </View>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[3] }}>
        {aoLigar && <Botao rotulo="Ligar" dica="Liga para o primeiro contato da rede de cuidado" aoTocar={aoLigar} />}
        <Botao rotulo="Ver a casa" variante="secundario" aoTocar={aoAbrir} />
      </View>
    </View>
  );
}

function Dado({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <View accessible accessibilityLabel={`${rotulo}: ${valor}`} style={{ minWidth: 120 }}>
      <Texto variante="pequeno" suave>
        {rotulo}
      </Texto>
      <Texto forte style={{ fontSize: 20 }}>
        {valor}
      </Texto>
    </View>
  );
}
