import { Stack, useLocalSearchParams } from 'expo-router';
import { ActivityIndicator, ScrollView, View } from 'react-native';

import { useAlerta, useReconhecer } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import type { AcaoDeReconhecimento } from '@/application/modelos';
import { Botao } from '@/componentes/Botao';
import { Farol } from '@/componentes/Farol';
import { Texto } from '@/componentes/Texto';
import { ativo, ROTULO_DO_ESTADO } from '@/dominio/alerta';
import { espaco, useTema } from '@/tema';
import { formatarHora } from '@/util/formato';

const ACOES: { acao: AcaoDeReconhecimento; rotulo: string; dica: string }[] = [
  { acao: 'VOU_LIGAR', rotulo: 'Vou ligar', dica: 'Avisa a rede que você vai ligar agora' },
  { acao: 'ESTOU_INDO', rotulo: 'Estou indo', dica: 'Avisa a rede que você está indo até a casa' },
  { acao: 'ELA_ESTA_BEM', rotulo: 'Ela está bem', dica: 'Encerra o alerta: você confirmou que ela está bem' },
];

export default function AlertaDetalhe() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const tema = useTema();
  const alerta = useAlerta(id);
  const reconhecer = useReconhecer(id);

  if (alerta.isPending) return <ActivityIndicator style={{ margin: espaco[6] }} color={tema.marca} />;
  if (alerta.isError) {
    return (
      <Texto accessibilityRole="alert" style={{ margin: espaco[5] }}>
        {mensagemDeErro(alerta.error)}
      </Texto>
    );
  }

  const a = alerta.data;
  return (
    <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
      <Stack.Screen options={{ title: a.apelidoDaCasa }} />
      <Farol nivel={a.nivel} />
      <Texto variante="subtitulo" forte>
        {ROTULO_DO_ESTADO[a.estado]}
      </Texto>
      {ativo(a) && <Texto>Responder até {formatarHora(a.prazo)}.</Texto>}

      <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[4] }}>
        Linha do tempo
      </Texto>
      <View accessibilityRole="list" style={{ gap: espaco[3] }}>
        <Evento momento={a.abertoEm} descricao="Alerta aberto" />
        {a.linhaDoTempo?.map((e) => (
          <Evento key={`${e.momento}-${e.degrau}`} momento={e.momento} descricao={e.descricao} />
        ))}
      </View>

      {ativo(a) && (
        <View style={{ gap: espaco[3], marginTop: espaco[4] }}>
          {ACOES.map(({ acao, rotulo, dica }) => (
            <Botao
              key={acao}
              rotulo={rotulo}
              dica={dica}
              variante={acao === 'ELA_ESTA_BEM' ? 'secundario' : 'primario'}
              aoTocar={() => reconhecer.mutate(acao)}
            />
          ))}
        </View>
      )}
      {reconhecer.isSuccess && <Texto accessibilityRole="alert">Pronto, a rede de cuidado foi avisada.</Texto>}
      {reconhecer.isError && <Texto accessibilityRole="alert">{mensagemDeErro(reconhecer.error)}</Texto>}
    </ScrollView>
  );
}

function Evento({ momento, descricao }: { momento: string; descricao: string }) {
  return (
    <View accessible style={{ flexDirection: 'row', gap: espaco[3] }}>
      <Texto forte style={{ minWidth: 56 }}>
        {formatarHora(momento)}
      </Texto>
      <Texto style={{ flex: 1 }}>{descricao}</Texto>
    </View>
  );
}
