import { router } from 'expo-router';
import { useStore } from 'zustand';
import { useState } from 'react';
import { Pressable, ScrollView, Switch, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { useCasas } from '@/presentation/hooks/consultas';
import { AvisoDemonstracao } from '@/presentation/componentes/AvisoDemonstracao';
import { Botao } from '@/presentation/componentes/Botao';
import { Texto } from '@/presentation/componentes/Texto';
import { bloqueioStore } from '@/application/estado/bloqueio';
import type { EscalaDoTexto } from '@/application/estado/preferencias';
import { reproducaoStore } from '@/application/estado/reproducao';
import { useEmDemonstracao, usePreferencias } from '@/presentation/hooks/estado';
import { useContainer } from '@/presentation/providers/container';
import { espaco, raio, toque, useTema } from '@/presentation/tema';

const OPCOES_DE_TEXTO: { escala: EscalaDoTexto; rotulo: string }[] = [
  { escala: 'NORMAL', rotulo: 'Normal' },
  { escala: 'GRANDE', rotulo: 'Grande' },
  { escala: 'MAIOR', rotulo: 'Maior' },
];

export default function Ajustes() {
  const tema = useTema();
  const casas = useCasas().data?.dados ?? [];
  const { sessao, demonstracao, bloqueio } = useContainer();
  const bloqueioAtivo = useStore(bloqueioStore, (b) => b.ativo);
  const [avisoDoBloqueio, setAvisoDoBloqueio] = useState<string | null>(null);
  const demo = useEmDemonstracao();
  const replayAtivo = useStore(reproducaoStore, (r) => r.ativo);
  const escalaDoTexto = usePreferencias((p) => p.escalaDoTexto);

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
              dica={replayAtivo ? 'Para o replay e volta ao exemplo' : 'Toca as 96 horas reais, uma hora a cada 1,5 segundo'}
              aoTocar={() => (replayAtivo ? demonstracao.pararReproducao() : demonstracao.reproduzirOndaReal())}
            />
            <Texto suave>
              Simulação (número inventado): a casa da Dona Lurdes vai a Perigo agora e, sem resposta em 8 segundos, chega
              o lembrete leve.
            </Texto>
            <Botao rotulo="Simular onda de calor" variante="secundario" dica="Leva a casa da Dona Lurdes a Perigo com um número inventado" aoTocar={() => demonstracao.simularOndaDeCalor()} />
            <Botao rotulo="Recomeçar o exemplo" variante="secundario" dica="Volta os dados de exemplo ao começo" aoTocar={() => demonstracao.recomecar()} />
          </View>
        )}

        <View style={{ gap: espaco[3] }}>
          <Texto variante="subtitulo" accessibilityRole="header">
            Tamanho do texto
          </Texto>
          <Texto suave>Vale para todo o app, neste celular, e soma com o tamanho de fonte do sistema.</Texto>
          <View accessibilityRole="radiogroup" accessibilityLabel="Tamanho do texto" style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[2] }}>
            {OPCOES_DE_TEXTO.map(({ escala, rotulo }) => {
              const escolhida = escalaDoTexto === escala;
              return (
                <Pressable
                  key={escala}
                  accessibilityRole="radio"
                  accessibilityState={{ checked: escolhida }}
                  accessibilityLabel={`Texto ${rotulo.toLowerCase()}`}
                  accessibilityHint="Muda o tamanho das letras em todo o app"
                  onPress={() => sessao.definirEscalaDoTexto(escala)}
                  style={{
                    minHeight: toque.minimo,
                    paddingHorizontal: espaco[4],
                    justifyContent: 'center',
                    borderRadius: raio.pilula,
                    borderWidth: 2,
                    borderColor: tema.marca,
                    backgroundColor: escolhida ? tema.marca : 'transparent',
                  }}
                >
                  <Texto forte style={{ color: escolhida ? tema.textoSobreMarca : tema.marca }}>
                    {rotulo}
                  </Texto>
                </Pressable>
              );
            })}
          </View>
        </View>

        <View style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[3] }}>
          <View style={{ flex: 1 }}>
            <Texto forte>Pedir digital ou rosto para abrir</Texto>
            <Texto variante="pequeno" suave>
              Protege CPF, saúde e telefones da rede neste celular. O Modo Idoso nunca pede.
            </Texto>
          </View>
          <Switch
            accessibilityLabel="Pedir digital ou rosto para abrir"
            accessibilityHint="Liga ou desliga a digital para abrir a área da família"
            value={bloqueioAtivo}
            onValueChange={async (v) => {
              setAvisoDoBloqueio(null);
              if (!v) return bloqueio.desativar();
              const r = await bloqueio.ativar();
              if (!r.ok) setAvisoDoBloqueio(r.erro.mensagem);
            }}
          />
        </View>
        {avisoDoBloqueio && <Texto accessibilityRole="alert">{avisoDoBloqueio}</Texto>}

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
