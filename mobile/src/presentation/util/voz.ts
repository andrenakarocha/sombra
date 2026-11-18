import * as Speech from 'expo-speech';

// Um pouco mais devagar que o normal, para quem ouve com dificuldade.
export function falar(texto: string) {
  Speech.stop();
  Speech.speak(texto, { language: 'pt-BR', rate: 0.9 });
}
