import { router } from 'expo-router';
import { useStore } from 'zustand';
import { ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useCasas } from '@/presentation/hooks/consultas';
import { AvisoDemonstracao } from '@/presentation/componentes/AvisoDemonstracao';
import { Botao } from '@/presentation/componentes/Botao';
import { Texto } from '@/presentation/componentes/Texto';
import { reproducaoStore } from '@/application/estado/reproducao';
import { useEmDemonstracao } from '@/presentation/hooks/estado';
import { useContainer } from '@/presentation/providers/container';
import { espaco, useTema } from '@/presentation/tema';

export default function Ajustes() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const { sessao, demonstracao } = useContainer();
  const demo = useEmDemonstracao();
  const replayAtivo = useStore(reproducaoStore, (r) => r.ativo);

  return (
    <SafeAreaView edges={['top']} style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Ajustes
        </Texto>
        <AvisoDemonstracao />

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
              aoTocar={() => sessao.definirModoIdoso(casa.id)}
            />
          ))}
        </View>

        {demo && (
          <View style={{ gap: espaco[3] }}>
            <Texto variante="subtitulo" accessibilityRole="header">
              Demonstração
            </Texto>
            <Texto suave>
              Dados reais: as 96 horas da onda de calor de 12 a 16 de novembro de 2023 na estação do INMET em Santana,
              uma hora a cada 1,5 segundo.
            </Texto>
            <Botao
              rotulo={replayAtivo ? 'Parar a onda de calor real' : 'Reproduzir a onda de calor real (INMET, nov/2023)'}
              aoTocar={() => (replayAtivo ? demonstracao.pararReproducao() : demonstracao.reproduzirOndaReal())}
            />
            <Texto suave>
              Simulação (número inventado): a casa da Dona Lurdes vai a Perigo agora e, sem resposta em 8 segundos, chega
              o lembrete leve.
            </Texto>
            <Botao rotulo="Simular onda de calor" variante="secundario" aoTocar={() => demonstracao.simularOndaDeCalor()} />
            <Botao rotulo="Recomeçar o exemplo" variante="secundario" aoTocar={() => demonstracao.recomecar()} />
          </View>
        )}

        <Botao
          rotulo="Central de sincronização"
          variante="secundario"
          dica="O que está esperando a internet e o que o Sombra não aceitou"
          aoTocar={() => router.push('/sincronizacao')}
        />

        <Botao
          rotulo={demo ? 'Sair da demonstração' : 'Sair da conta'}
          variante="secundario"
          dica="Sai da conta e apaga os dados salvos no aparelho"
          aoTocar={() => sessao.sair()}
        />
      </ScrollView>
    </SafeAreaView>
  );
}
