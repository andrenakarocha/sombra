import { router, Stack, useLocalSearchParams } from 'expo-router';
import { ActivityIndicator, ScrollView, View } from 'react-native';

import { useCasa, useLeituras24h, useMoradores, useRedeDeCuidado } from '@/api/consultas';
import { mensagemDeErro } from '@/api/mensagens';
import { AvisoOffline } from '@/componentes/AvisoOffline';
import { Botao } from '@/componentes/Botao';
import { Farol } from '@/componentes/Farol';
import { GraficoDeCalor } from '@/componentes/GraficoDeCalor';
import { Texto } from '@/componentes/Texto';
import { INFO_DO_NIVEL } from '@/dominio/nivel';
import { espaco, useTema } from '@/tema';
import { formatarGraus, formatarHora } from '@/util/formato';
import { ligar, podeLigar } from '@/util/ligar';

export default function Casa() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const tema = useTema();
  const casa = useCasa(id);
  const leituras = useLeituras24h(id);
  const moradores = useMoradores(id);
  const rede = useRedeDeCuidado(id);

  if (casa.isPending) return <ActivityIndicator style={{ margin: espaco[6] }} color={tema.marca} />;
  if (casa.isError) {
    return (
      <Texto accessibilityRole="alert" style={{ margin: espaco[5] }}>
        {mensagemDeErro(casa.error)}
      </Texto>
    );
  }

  const c = casa.data.dados;
  const morador = moradores.data?.[0];
  return (
    <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
      <Stack.Screen options={{ title: c.apelido }} />
      {casa.data.origem === 'cache' && <AvisoOffline atualizadoEm={casa.data.atualizadoEm} />}

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

      {leituras.data && <GraficoDeCalor leituras={leituras.data} nivel={c.nivelAtual} />}
      {leituras.isError && <Texto suave>O gráfico das últimas 24 horas precisa de conexão.</Texto>}

      {c.alertaAberto?.tipo === 'PREVENTIVO' && (
        <Texto forte>Previsão: calor perigoso nas próximas 24 horas.</Texto>
      )}
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
      {rede.isError && <Texto suave>{mensagemDeErro(rede.error)}</Texto>}
      {rede.data?.map((contato, i) => (
        <View key={contato.id} style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[3], flexWrap: 'wrap' }}>
          <View style={{ flex: 1, minWidth: 160 }}>
            <Texto forte>
              {i + 1}. {contato.nome}
            </Texto>
            <Texto suave>{contato.papel === 'VIZINHO' ? 'Quem está perto' : 'Família'}</Texto>
          </View>
          {podeLigar(contato.telefone) && (
            <Botao
              rotulo="Ligar"
              variante="secundario"
              dica={`Liga para ${contato.nome}`}
              aoTocar={() => ligar(contato.telefone)}
            />
          )}
        </View>
      ))}
    </ScrollView>
  );
}
