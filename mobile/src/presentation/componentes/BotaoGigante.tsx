import { Pressable } from 'react-native';

import { fonte, raio, tamanho, toque, useTema } from '@/presentation/tema';

import { Texto } from './Texto';

type Props = {
  rotulo: string;
  instrucao: string;
  variante: 'bem' | 'ajuda';
  // Com segurarPorMs, só aciona segurando: evita um pedido de ajuda por toque acidental.
  segurarPorMs?: number;
  aoAcionar: () => void;
};

// Botão do Modo Idoso: ≥ 120 dp de altura e o que fazer escrito no próprio botão.
export function BotaoGigante({ rotulo, instrucao, variante, segurarPorMs, aoAcionar }: Props) {
  const cores = useTema().risco[variante === 'bem' ? 'normal' : 'perigo'];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={rotulo}
      accessibilityHint={instrucao}
      onPress={segurarPorMs ? undefined : aoAcionar}
      onLongPress={segurarPorMs ? aoAcionar : undefined}
      delayLongPress={segurarPorMs}
      style={({ pressed }) => ({
        minHeight: toque.idoso,
        borderRadius: raio.grande,
        backgroundColor: cores.fundo,
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: 16,
        paddingVertical: 12,
        opacity: pressed ? 0.85 : 1,
      })}
    >
      <Texto
        style={{
          color: cores.texto,
          fontFamily: fonte.textoExtra,
          fontSize: tamanho.idosoBotao,
          lineHeight: Math.round(tamanho.idosoBotao * 1.2),
          textAlign: 'center',
        }}
      >
        {rotulo}
      </Texto>
      <Texto forte style={{ color: cores.texto, fontSize: 16, textAlign: 'center' }}>
        {instrucao}
      </Texto>
    </Pressable>
  );
}
