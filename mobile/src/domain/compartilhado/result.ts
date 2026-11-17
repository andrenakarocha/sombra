// Erros de regra como valor, não como exceção: quem chama é obrigado a tratar os dois caminhos.

export class ErroDeDominio {
  constructor(
    readonly codigo: string,
    readonly mensagem: string,
  ) {}
}

export type Result<T, E = ErroDeDominio> = { readonly ok: true; readonly valor: T } | { readonly ok: false; readonly erro: E };

export const ok = <T>(valor: T): Result<T, never> => ({ ok: true, valor });

export const falha = (codigo: string, mensagem: string): Result<never> => ({
  ok: false,
  erro: new ErroDeDominio(codigo, mensagem),
});

// Para fronteiras onde o valor já foi validado (fixtures, dados do próprio banco).
export function valorOuErro<T>(resultado: Result<T>): T {
  if (!resultado.ok) throw new Error(`${resultado.erro.codigo}: ${resultado.erro.mensagem}`);
  return resultado.valor;
}
