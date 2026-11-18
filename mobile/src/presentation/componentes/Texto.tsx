import { Text, type TextProps } from 'react-native';

import { ESCALAS_DO_TEXTO } from '@/application/estado/preferencias';
import { usePreferencias } from '@/presentation/hooks/estado';
import { fonte, tamanho, useTema } from '@/presentation/tema';

type Variante = 'pequeno' | 'texto' | 'subtitulo' | 'titulo' | 'tituloGrande' | 'display';

type Props = TextProps & {
  variante?: Variante;
  forte?: boolean;
  suave?: boolean;
};

// allowFontScaling fica no padrão (ligado): o texto acompanha o tamanho de fonte do sistema. O tamanho escolhido no
// app multiplica por cima, menos no `display` (o número gigante), que já é grande e precisa caber na tela.
export function Texto({ variante = 'texto', forte, suave, style, ...resto }: Props) {
  const tema = useTema();
  const escala = usePreferencias((p) => (variante === 'display' ? 1 : ESCALAS_DO_TEXTO[p.escalaDoTexto]));
  const titulo = variante === 'titulo' || variante === 'tituloGrande' || variante === 'display';
  const tamanhoDaFonte = Math.round(tamanho[variante] * escala);
  return (
    <Text
      style={[
        {
          color: suave ? tema.textoSuave : tema.texto,
          fontFamily: titulo ? fonte.display : forte ? fonte.textoForte : fonte.texto,
          fontSize: tamanhoDaFonte,
          lineHeight: Math.round(tamanhoDaFonte * (titulo ? 1.15 : 1.4)),
        },
        style,
      ]}
      {...resto}
    />
  );
}
