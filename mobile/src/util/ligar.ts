import { Alert, Linking } from 'react-native';

import { TOKEN_DEMO, useSessao } from '@/sessao/sessao';

// Um telefone mascarado (***) não liga: só o Cuidador da própria casa recebe o número inteiro.
export function podeLigar(telefone: string | null | undefined): telefone is string {
  return !!telefone && !telefone.includes('*');
}

export function ligar(telefone: string) {
  if (useSessao.getState().token === TOKEN_DEMO) {
    Alert.alert('Modo demonstração', 'Com os dados de exemplo, a ligação não é feita.');
    return;
  }
  return Linking.openURL(`tel:${telefone.replace(/[^\d+]/g, '')}`);
}
