import { router, Stack, useLocalSearchParams } from 'expo-router';
import { ActivityIndicator, RefreshControl, ScrollView, View } from 'react-native';

import { INFO_DO_NIVEL } from '@/domain/clima/nivel-de-risco';
import { AvisoOffline } from '@/presentation/componentes/AvisoOffline';
import { Botao } from '@/presentation/componentes/Botao';
import { Farol } from '@/presentation/componentes/Farol';
import { GraficoDeCalor } from '@/presentation/componentes/GraficoDeCalor';
import { Texto } from '@/presentation/componentes/Texto';
import { usePainelDaCasa } from '@/presentation/hooks/consultas';
import { espaco, useTema } from '@/presentation/tema';
import { formatarGraus, formatarHora } from '@/presentation/util/formato';
import { ligar, podeLigar } from '@/presentation/util/ligar';
import { mensagemDeErro } from '@/presentation/util/mensagens';

export default function Casa() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const tema = useTema();
  const painel = usePainelDaCasa(id);

  if (painel.isPending) return <ActivityIndicator style={{ margin: espaco[6] }} color={tema.marca} />;
  if (painel.isError) {
    return (
      <Texto accessibilityRole="alert" style={{ margin: espaco[5] }}>
        {mensagemDeErro(painel.error)}
      </Texto>
    );
  }

  const { casa: c, leituras24h, moradores, rede } = painel.data.dados;
  const morador = moradores[0];
  return (
    <ScrollView
      contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}
      refreshControl={<RefreshControl refreshing={painel.isRefetching} onRefresh={() => void painel.refetch()} />}
    >
      <Stack.Screen options={{ title: c.apelido }} />
      {painel.data.origem === 'local' && <AvisoOffline atualizadoEm={painel.data.atualizadoEm} />}

      <Farol nivel={c.nivelAtual} />
      <Texto>{INFO_DO_NIVEL[c.nivelAtual].frase}</Texto>
      {c.ultimaLeitura && (
        <Texto variante="display" accessibilityLabel={`Índice de calor ${formatarGraus(c.ultimaLeitura.indiceDeCalorC)} graus`}>
          {formatarGraus(c.ultimaLeitura.indiceDeCalorC)}
          <Texto variante="subtitulo">°C</Texto>
        </Texto>
      )}
      {c.ultimaLeitura && (
        <Texto suave>
          {formatarGraus(c.ultimaLeitura.temperaturaC)} °C · umidade {Math.round(c.ultimaLeitura.umidadePct)}% · às{' '}
          {formatarHora(c.ultimaLeitura.momento)}
        </Texto>
      )}

      <GraficoDeCalor leituras={leituras24h} nivel={c.nivelAtual} />

      {c.alertaAberto?.tipo === 'PREVENTIVO' && <Texto forte>Previsão: calor perigoso nas próximas 24 horas.</Texto>}
      {c.alertaAberto && (
        <Botao
          rotulo="Ver o alerta"
          dica={`Responder até ${formatarHora(c.alertaAberto.prazo)}`}
          aoTocar={() => router.push(`/alerta/${c.alertaAberto!.id}`)}
        />
      )}
      {morador && podeLigar(morador.telefone) && (
        <Botao rotulo={`Ligar para ${morador.nome}`} aoTocar={() => ligar(morador.telefone!)} />
      )}

      <Botao
        rotulo="Morador e dados de saúde"
        variante="secundario"
        dica="Cadastro com CPF, Cartão SUS e saúde, sempre mascarados"
        aoTocar={() => router.push(`/casa/${id}/morador`)}
      />

      <Texto variante="subtitulo" accessibilityRole="header" style={{ marginTop: espaco[4] }}>
        Rede de cuidado
      </Texto>
      {rede.length === 0 && <Texto suave>Nenhum contato na rede ainda.</Texto>}
      {rede.map((contato, i) => (
        <View key={contato.id} style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[3], flexWrap: 'wrap' }}>
          <View style={{ flex: 1, minWidth: 160 }}>
            <Texto forte>
              {i + 1}. {contato.nome}
            </Texto>
            <Texto suave>{contato.papel === 'VIZINHO' ? 'Quem está perto' : 'Família'}</Texto>
          </View>
          {podeLigar(contato.telefone) && (
            <Botao rotulo="Ligar" variante="secundario" dica={`Liga para ${contato.nome}`} aoTocar={() => ligar(contato.telefone)} />
          )}
        </View>
      ))}
    </ScrollView>
  );
}
