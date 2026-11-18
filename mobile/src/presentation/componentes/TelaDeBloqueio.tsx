import { useEffect } from 'react';
import { AppState, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useStore } from 'zustand';

import { bloqueioStore } from '@/application/estado/bloqueio';

import { useContainer } from '../providers/container';
import { espaco, useTema } from '../tema';

import { Botao } from './Botao';
import { Texto } from './Texto';

// Cobre a área da família quando o bloqueio está ligado e o app voltou de mais de 30 s fora.
export function TelaDeBloqueio() {
  const tema = useTema();
  const { bloqueio } = useContainer();
  const bloqueado = useStore(bloqueioStore, (b) => b.bloqueado);

  useEffect(() => {
    const assinatura = AppState.addEventListener('change', (estado) => {
      if (estado === 'background') bloqueio.foiParaSegundoPlano();
      if (estado === 'active') bloqueio.voltou();
    });
    return () => assinatura.remove();
  }, [bloqueio]);

  useEffect(() => {
    if (bloqueado) void bloqueio.desbloquear();
  }, [bloqueado, bloqueio]);

  if (!bloqueado) return null;
  return (
    <SafeAreaView
      accessibilityViewIsModal
      style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0, backgroundColor: tema.fundo, justifyContent: 'center' }}
    >
      <View style={{ padding: espaco[6], gap: espaco[5] }}>
        <Texto variante="tituloGrande" accessibilityRole="header">
          Sombra
        </Texto>
        <Texto>Os dados das casas estão protegidos. Use a digital ou o rosto para abrir.</Texto>
        <Botao rotulo="Desbloquear" dica="Pede a digital ou o rosto" aoTocar={() => void bloqueio.desbloquear()} />
      </View>
    </SafeAreaView>
  );
}
