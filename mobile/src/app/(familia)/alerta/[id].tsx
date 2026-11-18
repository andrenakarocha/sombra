import { Stack, useLocalSearchParams } from 'expo-router';
import { ActivityIndicator, ScrollView, View } from 'react-native';

import { useAlerta, useReconhecer } from '@/presentation/hooks/consultas';
import { mensagemDeErro } from '@/presentation/util/mensagens';
import type { AcaoDeReconhecimento } from '@/application/modelos';
import { Botao } from '@/presentation/componentes/Botao';
import { AvisoOffline } from '@/presentation/componentes/AvisoOffline';
import { Farol } from '@/presentation/componentes/Farol';
import { Texto } from '@/presentation/componentes/Texto';
import { ativo, ROTULO_DO_ESTADO } from '@/presentation/util/textos-do-alerta';
import { espaco, useTema } from '@/presentation/tema';
import { formatarHora } from '@/presentation/util/formato';

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

  const a = alerta.data.dados;
  return (
    <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
      <Stack.Screen options={{ title: a.apelidoDaCasa }} />
      <Farol nivel={a.nivel} />
      <Texto variante="subtitulo" forte>
        {ROTULO_DO_ESTADO[a.estado]}
      </Texto>
      {ativo(a) && a.estado !== 'RECONHECIDO' && <Texto>Responder até {formatarHora(a.prazo)}.</Texto>}
      {a.minutosAteProximoDegrau != null && (
        <Texto forte accessibilityLiveRegion="polite">
          {a.minutosAteProximoDegrau === 0
            ? 'O próximo aviso da rede sai agora.'
            : `Sem resposta, o próximo aviso da rede sai em ${a.minutosAteProximoDegrau} min.`}
        </Texto>
      )}
      {alerta.data.origem === 'local' && <AvisoOffline atualizadoEm={alerta.data.atualizadoEm} />}

      <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[4] }}>
        Linha do tempo
      </Texto>
      <View accessibilityRole="list" style={{ gap: espaco[3] }}>
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
      {reconhecer.isSuccess && (
        <Texto accessibilityRole="alert">
          {reconhecer.data === 'NA_FILA'
            ? 'Guardado. Sem internet agora: a rede é avisada assim que a internet voltar.'
            : reconhecer.data === 'RECUSADO'
              ? 'O Sombra não aceitou esta resposta. Veja em Ajustes → Sincronização.'
              : 'Pronto, a rede de cuidado foi avisada.'}
        </Texto>
      )}
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
