import { podeUsarOPainel, sessao } from '@/api/sessao';

function jwt(payload: object): string {
  const b64 = (o: object) => btoa(unescape(encodeURIComponent(JSON.stringify(o)))).replace(/=+$/, '');
  return `${b64({ alg: 'HS256' })}.${b64(payload)}.assinatura`;
}

describe('sessão do operador', () => {
  beforeEach(() => sessionStorage.clear());

  it('lê o nome e o papel do token e guarda no sessionStorage', () => {
    const s = sessao.entrar(jwt({ name: 'Operação do Sombra', role: 'Operador', exp: Date.now() / 1000 + 3600 }));
    expect(s.papel).toBe('Operador');
    expect(s.nome).toBe('Operação do Sombra');
    expect(sessao.atual()?.token).toBe(s.token);
    expect(localStorage.length).toBe(0);
  });

  it('esquece um token vencido', () => {
    sessao.entrar(jwt({ name: 'x', role: 'Operador', exp: Date.now() / 1000 - 1 }));
    expect(sessao.atual()).toBeNull();
  });

  it('o painel é de Operador e Admin; o Cuidador usa o app', () => {
    expect(podeUsarOPainel('Operador')).toBe(true);
    expect(podeUsarOPainel('Admin')).toBe(true);
    expect(podeUsarOPainel('Cuidador')).toBe(false);
  });
});
