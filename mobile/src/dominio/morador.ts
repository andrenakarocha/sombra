import type { NovoMorador } from '@/application/modelos';
import { CartaoSus } from '@/domain/morador/cartao-sus';
import { Cpf } from '@/domain/morador/cpf';
import { Telefone } from '@/domain/morador/telefone';

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
  // Os value objects do domínio dizem o que está errado, na linguagem da pessoa.
  for (const [preenchido, resultado] of [
    [!editando || !!f.cpf, () => Cpf.criar(f.cpf)],
    [!!f.cartaoSus, () => CartaoSus.criar(f.cartaoSus)],
    [!!f.telefone, () => Telefone.criar(f.telefone)],
  ] as const) {
    if (!preenchido) continue;
    const r = resultado();
    if (!r.ok) return r.erro.mensagem;
  }
  return null;
}
