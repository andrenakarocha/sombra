import { ActivityIndicator, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useCasas, usePrivacidade } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import { AvisoDemonstracao } from '@/componentes/AvisoDemonstracao';
import { FarolDePrivacidade } from '@/componentes/FarolDePrivacidade';
import { SeletorDeCasa, useCasaEscolhida } from '@/componentes/SeletorDeCasa';
import { Texto } from '@/componentes/Texto';
import { espaco, raio, useTema } from '@/tema';
import { formatarHora } from '@/util/formato';

export default function Privacidade() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const { casa, escolher } = useCasaEscolhida(casas);
  const privacidade = usePrivacidade(casa?.id);

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Privacidade
        </Texto>
        <AvisoDemonstracao />
        <Texto suave>
          O Sombra guarda CPF, Cartão SUS e dados de saúde com cuidado. Aqui você vê se alguém tentou usar esses dados
          do jeito errado.
        </Texto>
        <SeletorDeCasa casas={casas} escolhida={casa} aoEscolher={escolher} />

        {privacidade.isPending && casa && <ActivityIndicator accessibilityLabel="Carregando" color={tema.marca} />}
        {privacidade.isError && <Texto accessibilityRole="alert">{mensagemDeErro(privacidade.error)}</Texto>}
        {privacidade.data && (
          <>
            <FarolDePrivacidade farol={privacidade.data.farol} />
            {privacidade.data.mensagens.length === 0 && <Texto>Nenhum problema com os dados desta casa.</Texto>}
            {privacidade.data.mensagens.map((m) => (
              <View
                key={`${m.momento}-${m.mensagemCidadao}`}
                style={{ backgroundColor: tema.superficie, borderRadius: raio.medio, padding: espaco[4], gap: espaco[2] }}
              >
                <Texto>{m.mensagemCidadao}</Texto>
                <Texto variante="pequeno" suave>
                  {formatarHora(m.momento)} · {m.fonte === 'IA' ? 'explicado pela IA, sem dados pessoais' : 'mensagem padrão'}
                </Texto>
              </View>
            ))}
          </>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}
