import * as Haptics from 'expo-haptics';
import { useEffect, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { registrarCheckIn } from '@/api/check-in';
import { useCasa } from '@/api/consultas';
import type { SituacaoDoCheckIn } from '@/api/tipos';
import { AvisoDemonstracao } from '@/componentes/AvisoDemonstracao';
import { Botao } from '@/componentes/Botao';
import { BotaoGigante } from '@/componentes/BotaoGigante';
import { Texto } from '@/componentes/Texto';
import { proximoPasso } from '@/dominio/alerta';
import { INFO_DO_NIVEL } from '@/dominio/nivel';
import { usePreferencias } from '@/sessao/preferencias';
import { espaco, fonte, tamanho, toque, useTema } from '@/tema';
import { ligar } from '@/util/ligar';
import { falar } from '@/util/voz';

type Resposta = { situacao: SituacaoDoCheckIn; resultado: 'ENVIADO' | 'NA_FILA' };

const CONFIRMACAO: Record<SituacaoDoCheckIn, Record<Resposta['resultado'], string>> = {
  ESTOU_BEM: {
    ENVIADO: 'Obrigado! Sua família já sabe que você está bem.',
    NA_FILA: 'Guardado. Sem internet agora: o aviso vai assim que a internet voltar.',
  },
  PRECISO_DE_AJUDA: {
    ENVIADO: 'Pedido enviado. Sua família foi avisada e vai entrar em contato.',
    NA_FILA: 'Sem internet: o pedido vai assim que a internet voltar. Se for urgente, ligue 192.',
  },
};

export default function ModoIdoso() {
  const tema = useTema();
  const casaId = usePreferencias((p) => p.modoIdosoCasaId)!;
  const casa = useCasa(casaId).data?.dados;
  const [resposta, setResposta] = useState<Resposta | null>(null);

  const nivel = casa?.nivelAtual ?? 'NORMAL';
  const graus = casa?.ultimaLeitura ? Math.round(casa.ultimaLeitura.temperaturaC) : null;
  const situacao = `${graus !== null ? `Sua casa está com ${graus} graus. ` : ''}${INFO_DO_NIVEL[nivel].frase} ${proximoPasso(casa)}`;

  // Lê a situação ao abrir e quando o nível muda.
  useEffect(() => {
    if (casa) falar(situacao);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [!!casa, nivel]);

  async function responder(s: SituacaoDoCheckIn) {
    const resultado = await registrarCheckIn(casaId, s);
    setResposta({ situacao: s, resultado });
    Haptics.notificationAsync(
      resultado === 'ENVIADO' ? Haptics.NotificationFeedbackType.Success : Haptics.NotificationFeedbackType.Warning,
    );
    falar(CONFIRMACAO[s][resultado]);
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: tema.fundo }}>
      <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4], flexGrow: 1 }}>
        <AvisoDemonstracao />
        <Texto variante="subtitulo" suave style={{ textAlign: 'center' }}>
          Sua casa agora
        </Texto>
        {graus !== null && (
          <Texto
            accessibilityLabel={`${graus} graus`}
            maxFontSizeMultiplier={1.3}
            style={{
              fontFamily: fonte.display,
              fontSize: tamanho.idosoNumero,
              lineHeight: Math.round(tamanho.idosoNumero * 1.05),
              textAlign: 'center',
              color: tema.risco[INFO_DO_NIVEL[nivel].token].borda,
            }}
          >
            {graus}°
          </Texto>
        )}
        <Texto forte style={{ fontSize: tamanho.idosoTexto, lineHeight: 34, textAlign: 'center' }}>
          {INFO_DO_NIVEL[nivel].frase}
        </Texto>

        {resposta ? (
          <View accessibilityRole="alert" style={{ gap: espaco[4] }}>
            <Texto forte style={{ fontSize: tamanho.idosoTexto, lineHeight: 34, textAlign: 'center' }}>
              {CONFIRMACAO[resposta.situacao][resposta.resultado]}
            </Texto>
            {resposta.situacao === 'PRECISO_DE_AJUDA' && resposta.resultado === 'NA_FILA' && (
              <Botao rotulo="Ligar 192 (SAMU)" aoTocar={() => ligar('192')} />
            )}
            <Botao rotulo="Voltar" variante="secundario" aoTocar={() => setResposta(null)} />
          </View>
        ) : (
          <>
            <BotaoGigante
              rotulo="ESTOU BEM"
              instrucao="aperte uma vez"
              variante="bem"
              aoAcionar={() => responder('ESTOU_BEM')}
            />
            <BotaoGigante
              rotulo="PRECISO DE AJUDA"
              instrucao="segure por 2 segundos"
              variante="ajuda"
              segurarPorMs={2000}
              aoAcionar={() => responder('PRECISO_DE_AJUDA')}
            />
            <Texto suave style={{ fontSize: 18, textAlign: 'center' }}>
              {proximoPasso(casa)}
            </Texto>
          </>
        )}

        <View style={{ flex: 1 }} />
        <Botao rotulo="Ouvir de novo" variante="secundario" aoTocar={() => falar(situacao)} />
        <SairDoModoIdoso />
      </ScrollView>
    </SafeAreaView>
  );
}

// Segurar 3 segundos evita sair sem querer; quem configura o celular é a família.
function SairDoModoIdoso() {
  const tema = useTema();
  const definirModoIdoso = usePreferencias((p) => p.definirModoIdoso);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel="Modo família"
      accessibilityHint="Segure por 3 segundos para voltar ao modo da família"
      delayLongPress={3000}
      onLongPress={() => definirModoIdoso(null)}
      style={{ minHeight: toque.minimo, alignItems: 'center', justifyContent: 'center' }}
    >
      <Texto variante="pequeno" style={{ color: tema.textoSuave }}>
        Modo família (segure)
      </Texto>
    </Pressable>
  );
}
