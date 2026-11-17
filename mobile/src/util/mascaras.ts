// Máscaras de digitação e de exibição da PII do morador (spec 07). O app nunca loga esses valores.

const digitos = (valor: string) => valor.replace(/\D/g, '');

export const PADRAO = {
  cpf: '###.###.###-##',
  cartaoSus: '### #### #### ####',
  telefone: '(##) #####-####',
} as const;

// Formata enquanto a pessoa digita: "5299822" → "529.982.2".
export function aplicarPadrao(valor: string, padrao: string) {
  const d = digitos(valor);
  let i = 0;
  let saida = '';
  for (const c of padrao) {
    if (i >= d.length) break;
    saida += c === '#' ? d[i++] : c;
  }
  return saida;
}

const jaMascarado = (valor: string) => valor.includes('*') || valor.startsWith('[');

// Defesa em profundidade: se a API devolver um valor inteiro, a tela mascara no formato do contrato (core-api.md).
export function exibirCpf(cpf: string) {
  if (jaMascarado(cpf)) return cpf;
  const d = digitos(cpf);
  return `***.${d.slice(3, 6)}.${d.slice(6, 9)}-**`;
}

export function exibirCartaoSus(cns: string) {
  return jaMascarado(cns) ? cns : `*** **** **** ${digitos(cns).slice(-4)}`;
}

export function exibirTelefone(telefone: string) {
  if (jaMascarado(telefone)) return telefone;
  const d = digitos(telefone);
  return `(${d.slice(0, 2)}) *****-${d.slice(-4)}`;
}

export function exibirSaude(perfil: string) {
  return jaMascarado(perfil) ? perfil : '[DADO DE SAÚDE PROTEGIDO]';
}
