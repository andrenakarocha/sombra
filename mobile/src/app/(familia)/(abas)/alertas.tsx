import { router } from 'expo-router';
import { ActivityIndicator, Pressable, RefreshControl, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useAlertas } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import type { Alerta } from '@/api/tipos';
import { AvisoDemonstracao } from '@/componentes/AvisoDemonstracao';
import { AvisoOffline } from '@/componentes/AvisoOffline';
import { Farol } from '@/componentes/Farol';
import { Texto } from '@/componentes/Texto';
import { ativo, ROTULO_DO_ESTADO } from '@/dominio/alerta';
import { espaco, raio, toque, useTema } from '@/tema';
import { formatarHora } from '@/util/formato';

export default function Alertas() {
  const tema = useTema();
  const alertas = useAlertas();
  const lista = alertas.data?.dados ?? [];
  const ativos = lista.filter(ativo);
  const resolvidos = lista.filter((a) => !ativo(a));

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView
        contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}
        refreshControl={<RefreshControl refreshing={alertas.isRefetching} onRefresh={() => void alertas.refetch()} />}
      >
        <Texto variante="tituloGrande" accessibilityRole="header">
          Alertas
        </Texto>
        <AvisoDemonstracao />

        {alertas.data?.origem === 'cache' && <AvisoOffline atualizadoEm={alertas.data.atualizadoEm} />}
        {alertas.isPending && <ActivityIndicator accessibilityLabel="Carregando os alertas" color={tema.marca} />}
        {alertas.isError && <Texto accessibilityRole="alert">{mensagemDeErro(alertas.error)}</Texto>}
        {alertas.isSuccess && ativos.length === 0 && <Texto suave>Nenhum alerta ativo. Tudo calmo por aqui.</Texto>}

        {ativos.map((a) => (
          <ItemDoAlerta key={a.id} alerta={a} />
        ))}
        {resolvidos.length > 0 && (
          <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[4] }}>
            Resolvidos
          </Texto>
        )}
        {resolvidos.map((a) => (
          <ItemDoAlerta key={a.id} alerta={a} />
        ))}
      </ScrollView>
    </SafeAreaView>
  );
}

function ItemDoAlerta({ alerta }: { alerta: Alerta }) {
  const tema = useTema();
  const estado = ROTULO_DO_ESTADO[alerta.estado];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${alerta.apelidoDaCasa}. ${estado}. Aberto às ${formatarHora(alerta.abertoEm)}`}
      accessibilityHint="Abre a linha do tempo do alerta"
      onPress={() => router.push(`/alerta/${alerta.id}`)}
      style={({ pressed }) => ({
        minHeight: toque.minimo,
        backgroundColor: tema.superficie,
        borderColor: tema.borda,
        borderWidth: 1,
        borderRadius: raio.medio,
        padding: espaco[4],
        gap: espaco[2],
        opacity: pressed ? 0.85 : 1,
      })}
    >
      <Texto forte>{alerta.apelidoDaCasa}</Texto>
      <Farol nivel={alerta.nivel} />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[3] }}>
        <Texto>{estado}</Texto>
        <Texto suave>aberto às {formatarHora(alerta.abertoEm)}</Texto>
      </View>
    </Pressable>
  );
}
