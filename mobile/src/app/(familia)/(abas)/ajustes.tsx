import { ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { sair } from '@/api/auth';
import { useCasas } from '@/api/consultas';
import { Botao } from '@/componentes/Botao';
import { Texto } from '@/componentes/Texto';
import { usePreferencias } from '@/sessao/preferencias';
import { espaco, useTema } from '@/tema';

export default function Ajustes() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const definirModoIdoso = usePreferencias((p) => p.definirModoIdoso);

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Ajustes
        </Texto>

        <View style={{ gap: espaco[3] }}>
          <Texto variante="subtitulo" accessibilityRole="header">
            Modo Idoso
          </Texto>
          <Texto suave>
            Para o celular de quem mora na casa: uma tela só, com letras grandes, voz e o botão ESTOU BEM. Para voltar,
            segure &quot;Modo família&quot; por 3 segundos.
          </Texto>
          {casas.map((casa) => (
            <Botao
              key={casa.id}
              rotulo={`Usar neste celular: ${casa.apelido}`}
              dica="Este celular passa a abrir só no Modo Idoso desta casa"
              aoTocar={() => definirModoIdoso(casa.id)}
            />
          ))}
        </View>

        <Botao
          rotulo="Sair da conta"
          variante="secundario"
          dica="Sai da conta e apaga os dados salvos no aparelho"
          aoTocar={sair}
        />
      </ScrollView>
    </SafeAreaView>
  );
}
