import { ActivityIndicator, Linking, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { pontosAPartirDe } from '@/application/casos-de-uso/perto';
import type { PontoDeResfriamento } from '@/application/modelos';
import { AvisoDemonstracao } from '@/presentation/componentes/AvisoDemonstracao';
import { Botao } from '@/presentation/componentes/Botao';
import { SeletorDeCasa, useCasaEscolhida } from '@/presentation/componentes/SeletorDeCasa';
import { SetaParaOPonto } from '@/presentation/componentes/SetaParaOPonto';
import { Texto } from '@/presentation/componentes/Texto';
import { espaco, raio, useTema } from '@/presentation/tema';
import { useCasas, usePontosDeResfriamento } from '@/presentation/hooks/consultas';
import { useLocalizacao } from '@/presentation/hooks/localizacao';
import { abertoAgora, formatarKm } from '@/presentation/util/formato';
import { mensagemDeErro } from '@/presentation/util/mensagens';

function abrirRota(ponto: PontoDeResfriamento, bairro: string) {
  const destino = ponto.lat !== undefined && ponto.lon !== undefined ? `${ponto.lat},${ponto.lon}` : `${ponto.nome}, ${bairro}, São Paulo`;
  return Linking.openURL(`https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(destino)}`);
}

export default function Perto() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const { casa, escolher } = useCasaEscolhida(casas);
  const pontos = usePontosDeResfriamento(casa?.id);
  const { posicao, direcao } = useLocalizacao();
  const lista = pontosAPartirDe(pontos.data ?? [], posicao);
  const maisPerto = lista[0];

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Perto de mim
        </Texto>
        <AvisoDemonstracao />
        <Texto suave>Lugares frescos e públicos perto da casa, para passar a hora mais quente do dia.</Texto>
        <SeletorDeCasa casas={casas} escolhida={casa} aoEscolher={escolher} />

        {pontos.isPending && casa && <ActivityIndicator accessibilityLabel="Carregando os pontos" color={tema.marca} />}
        {pontos.isError && <Texto accessibilityRole="alert">{mensagemDeErro(pontos.error)}</Texto>}
        {pontos.data?.length === 0 && <Texto suave>Nenhum ponto de resfriamento a até 3 km.</Texto>}

        {maisPerto?.rumoGraus != null && direcao !== null ? (
          <SetaParaOPonto nome={maisPerto.nome} rumoGraus={maisPerto.rumoGraus} direcaoGraus={direcao} distanciaKm={maisPerto.distanciaKm} />
        ) : (
          pontos.isSuccess && (
            <Texto suave>
              {posicao ? 'Gire o celular devagar para a bússola acordar.' : 'Com a localização ligada, aparece a direção até o mais perto. Sem ela, as distâncias são a partir da casa.'}
            </Texto>
          )
        )}

        {lista.map((p) => {
          const aberto = abertoAgora(p.horario.abre, p.horario.fecha);
          return (
            <View
              key={p.id}
              style={{
                backgroundColor: tema.superficie,
                borderColor: tema.borda,
                borderWidth: 1,
                borderRadius: raio.medio,
                padding: espaco[4],
                gap: espaco[2],
              }}
            >
              <Texto forte variante="subtitulo">
                {p.nome}
              </Texto>
              <Texto>
                {formatarKm(p.distanciaKm)} {p.distanciaDoAparelho ? 'daqui' : 'da casa'} · {aberto ? `aberto até ${p.horario.fecha}` : `fechado · abre às ${p.horario.abre}`}
              </Texto>
              <Texto suave>{p.acessivel ? '♿ Acessível para cadeira de rodas' : 'Sem informação de acessibilidade'}</Texto>
              <Texto variante="pequeno" suave>
                Fonte: {p.fonte}
              </Texto>
              <Botao rotulo="Abrir rota" dica={`Abre o mapa com o caminho até ${p.nome}`} aoTocar={() => abrirRota(p, casa!.bairro)} />
            </View>
          );
        })}
      </ScrollView>
    </SafeAreaView>
  );
}
