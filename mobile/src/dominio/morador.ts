import type { NovoMorador } from '@/api/tipos';
import { cpfValido } from '@/util/mascaras';

export type Formulario = Omit<NovoMorador, 'idade'> & { idade: string };

export const VAZIO: Formulario = {
  nome: '',
  idade: '',
  cpf: '',
  cartaoSus: '',
  telefone: '',
  perfilDeSaude: '',
  fatoresDeRisco: [],
};

// Na edição, CPF, Cartão SUS, telefone e saúde voltam mascarados: campo vazio quer dizer "manter como está".
export function validar(f: Formulario, editando: boolean): string | null {
  if (!f.nome.trim()) return 'Escreva o nome.';
  const idade = Number(f.idade);
  if (!Number.isInteger(idade) || idade < 0 || idade > 130) return 'Confira a idade.';
  if ((!editando || f.cpf) && !cpfValido(f.cpf)) return 'O CPF não é válido. Confira os números.';
  if (f.cartaoSus && f.cartaoSus.replace(/\D/g, '').length !== 15) return 'O Cartão SUS tem 15 números.';
  if (f.telefone && f.telefone.replace(/\D/g, '').length < 10) return 'Confira o telefone com DDD.';
  return null;
}
