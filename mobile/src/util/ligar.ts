import { Linking } from 'react-native';

// Um telefone mascarado (***) não liga: só o Cuidador da própria casa recebe o número inteiro.
export function podeLigar(telefone: string | null | undefined): telefone is string {
  return !!telefone && !telefone.includes('*');
}

export function ligar(telefone: string) {
  return Linking.openURL(`tel:${telefone.replace(/[^\d+]/g, '')}`);
}
