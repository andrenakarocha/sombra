/**
 * A sessão do operador: o JWT do Core fica no sessionStorage (some ao fechar a aba) e nunca no localStorage. O papel
 * vem do próprio token; o Core confere de novo em cada chamada.
 */
const CHAVE = 'sombra.painel.token';

export type Papel = 'Operador' | 'Admin' | 'Cuidador' | 'Servico';

export interface Sessao {
  token: string;
  nome: string;
  papel: Papel;
  expiraEm: number;
}

function decodificar(token: string): Sessao | null {
  try {
    const parte = token.split('.')[1];
    if (!parte) return null;
    const json = JSON.parse(decodeURIComponent(escape(atob(parte.replace(/-/g, '+').replace(/_/g, '/')))));
    return { token, nome: String(json.name ?? ''), papel: json.role as Papel, expiraEm: Number(json.exp) * 1000 };
  } catch {
    return null;
  }
}

export const sessao = {
  atual(): Sessao | null {
    const token = sessionStorage.getItem(CHAVE);
    const s = token ? decodificar(token) : null;
    if (!s || s.expiraEm <= Date.now()) {
      sessionStorage.removeItem(CHAVE);
      return null;
    }
    return s;
  },
  entrar(token: string): Sessao {
    const s = decodificar(token);
    if (!s) throw new Error('Token inválido.');
    sessionStorage.setItem(CHAVE, token);
    return s;
  },
  sair(): void {
    sessionStorage.removeItem(CHAVE);
  },
};

/** O painel é para quem acompanha muitas casas: Operador e Admin. */
export function podeUsarOPainel(papel: Papel): boolean {
  return papel === 'Operador' || papel === 'Admin';
}
