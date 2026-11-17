import { ActivityIndicator, RefreshControl, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { sair } from '@/api/auth';
import { useCasas } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import { Botao } from '@/componentes/Botao';
import { CartaoCasa } from '@/componentes/CartaoCasa';
import { Texto } from '@/componentes/Texto';
import { espaco, raio, useTema } from '@/tema';
import { formatarHora } from '@/util/formato';

// Lista das casas (F6.2). As abas da família substituem esta tela na F6.3.
export default function Inicio() {
  const tema = useTema();
  const casas = useCasas();
  const nada = () => {};

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView
        contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}
        refreshControl={<RefreshControl refreshing={casas.isRefetching} onRefresh={() => void casas.refetch()} />}
      >
        <Texto variante="tituloGrande" accessibilityRole="header">
          Suas casas
        </Texto>

        {casas.data?.origem === 'cache' && (
          <View
            accessibilityRole="alert"
            style={{ backgroundColor: tema.superficieAlternativa, borderRadius: raio.medio, padding: espaco[4] }}
          >
            <Texto forte>Sem conexão.</Texto>
            <Texto>Mostrando os dados salvos às {formatarHora(casas.data.atualizadoEm)}.</Texto>
          </View>
        )}

        {casas.isPending && <ActivityIndicator accessibilityLabel="Carregando as casas" color={tema.marca} />}
        {casas.isError && <Texto accessibilityRole="alert">{mensagemDeErro(casas.error)}</Texto>}

        {casas.data?.dados.map((casa) => (
          <CartaoCasa key={casa.id} casa={casa} aoAbrir={nada} />
        ))}

        <Botao rotulo="Sair" variante="secundario" dica="Sai da conta e apaga os dados salvos no aparelho" aoTocar={sair} />
      </ScrollView>
    </SafeAreaView>
  );
}
