import * as Location from 'expo-location';

import type { Localizacao } from '@/application/ports/servicos';

async function permitido() {
  const { status } = await Location.requestForegroundPermissionsAsync();
  return status === 'granted';
}

export const localizacaoExpo: Localizacao = {
  async atual() {
    if (!(await permitido())) return null;
    // A última posição conhecida responde na hora; a precisa vem depois, se der.
    const rapida = await Location.getLastKnownPositionAsync();
    const posicao = rapida ?? (await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }).catch(() => null));
    return posicao && { lat: posicao.coords.latitude, lon: posicao.coords.longitude };
  },
  async observarDirecao(ouvinte) {
    if (!(await permitido())) return () => {};
    const assinatura = await Location.watchHeadingAsync((h) => ouvinte(h.trueHeading >= 0 ? h.trueHeading : h.magHeading));
    return () => assinatura.remove();
  },
};
