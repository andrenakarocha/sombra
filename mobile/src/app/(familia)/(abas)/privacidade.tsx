import { useState } from 'react';
import { ActivityIndicator, ScrollView, Share, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useCasas, usePrivacidade } from '@/presentation/hooks/consultas';
import { mensagemDeErro } from '@/presentation/util/mensagens';
import { AvisoDemonstracao } from '@/presentation/componentes/AvisoDemonstracao';
import { FarolDePrivacidade } from '@/presentation/componentes/FarolDePrivacidade';
import { Botao } from '@/presentation/componentes/Botao';
import { SeletorDeCasa, useCasaEscolhida } from '@/presentation/componentes/SeletorDeCasa';
import { Texto } from '@/presentation/componentes/Texto';
import { espaco, raio, useTema } from '@/presentation/tema';
import { useContainer } from '@/presentation/providers/container';
import { formatarHora } from '@/presentation/util/formato';

export default function Privacidade() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const { casa, escolher } = useCasaEscolhida(casas);
  const privacidade = usePrivacidade(casa?.id);
  const { privacidadeLocal } = useContainer();
  const [confirmandoApagar, setConfirmandoApagar] = useState(false);

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

        <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[5] }}>
          Seus dados neste celular
        </Texto>
        <Texto suave>
          Pela LGPD (art. 18), você pode ver o que o Sombra guarda aqui e apagar tudo. CPF, Cartão SUS e telefones saem
          mascarados; os dados completos ficam no servidor.
        </Texto>
        <Botao
          rotulo="Exportar os dados (JSON)"
          variante="secundario"
          dica="Abre o compartilhar do celular com um arquivo de texto"
          aoTocar={async () => Share.share({ title: 'Dados do Sombra', message: await privacidadeLocal.exportar() })}
        />
        <Botao
          rotulo={confirmandoApagar ? 'Toque de novo para apagar tudo' : 'Apagar tudo deste celular'}
          variante={confirmandoApagar ? 'primario' : 'secundario'}
          dica="Sai da conta e apaga casas, alertas, fila e preferências deste aparelho"
          aoTocar={() => (confirmandoApagar ? privacidadeLocal.apagarTudo() : setConfirmandoApagar(true))}
        />
      </ScrollView>
    </SafeAreaView>
  );
}
