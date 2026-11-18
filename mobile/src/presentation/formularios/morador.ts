import type { NovoMorador } from '@/application/modelos';

// Estado do formulário: tudo texto, como a pessoa digita. Quem valida é o caso de uso (value objects do domínio).
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

export function paraNovoMorador({ idade, ...resto }: Formulario): NovoMorador {
  return { ...resto, idade: idade.trim() === '' ? Number.NaN : Number(idade) };
}
