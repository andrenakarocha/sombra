import { router } from 'expo-router';
import { ActivityIndicator, RefreshControl, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useCasas } from '@/presentation/hooks/consultas';
import { mensagemDeErro } from '@/presentation/util/mensagens';
import { AvisoDemonstracao } from '@/presentation/componentes/AvisoDemonstracao';
import { AvisoOffline } from '@/presentation/componentes/AvisoOffline';
import { CartaoCasa } from '@/presentation/componentes/CartaoCasa';
import { Texto } from '@/presentation/componentes/Texto';
import { espaco, useTema } from '@/presentation/tema';

export default function Inicio() {
  const tema = useTema();
  const casas = useCasas();

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView
        contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}
        refreshControl={<RefreshControl refreshing={casas.isRefetching} onRefresh={() => void casas.refetch()} />}
      >
        <Texto variante="tituloGrande" accessibilityRole="header">
          Suas casas
        </Texto>
        <AvisoDemonstracao />

        {casas.data?.origem === 'local' && <AvisoOffline atualizadoEm={casas.data.atualizadoEm} />}
        {casas.isPending && <ActivityIndicator accessibilityLabel="Carregando as casas" color={tema.marca} />}
        {casas.isError && <Texto accessibilityRole="alert">{mensagemDeErro(casas.error)}</Texto>}
        {casas.data?.dados.length === 0 && <Texto suave>Nenhuma casa na sua rede de cuidado ainda.</Texto>}

        {casas.data?.dados.map((casa) => (
          <CartaoCasa key={casa.id} casa={casa} aoAbrir={() => router.push(`/casa/${casa.id}`)} />
        ))}
      </ScrollView>
    </SafeAreaView>
  );
}
