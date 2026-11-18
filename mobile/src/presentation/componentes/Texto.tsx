import { Text, type TextProps } from 'react-native';

import { fonte, tamanho, useTema } from '@/presentation/tema';

type Variante = 'pequeno' | 'texto' | 'subtitulo' | 'titulo' | 'tituloGrande' | 'display';

type Props = TextProps & {
  variante?: Variante;
  forte?: boolean;
  suave?: boolean;
};

// allowFontScaling fica no padrão (ligado): o texto acompanha o tamanho de fonte do sistema.
export function Texto({ variante = 'texto', forte, suave, style, ...resto }: Props) {
  const tema = useTema();
  const titulo = variante === 'titulo' || variante === 'tituloGrande' || variante === 'display';
  return (
    <Text
      style={[
        {
          color: suave ? tema.textoSuave : tema.texto,
          fontFamily: titulo ? fonte.display : forte ? fonte.textoForte : fonte.texto,
          fontSize: tamanho[variante],
          lineHeight: Math.round(tamanho[variante] * (titulo ? 1.15 : 1.4)),
        },
        style,
      ]}
      {...resto}
    />
  );
}
