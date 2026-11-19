import { useState, type FormEvent } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { pedir } from '@/api/http';
import { LoginSchema } from '@/api/schemas';
import { podeUsarOPainel, sessao } from '@/api/sessao';
import { ErroNaTela } from '@/componentes/Base';
import { Folhagem, Marca } from '@/componentes/Icone';

/** A entrada do operador. O Cuidador usa o app; o painel é de quem acompanha muitas casas. */
export function Login() {
  const navegar = useNavigate();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<unknown>(null);
  const [enviando, setEnviando] = useState(false);

  async function entrar(evento: FormEvent) {
    evento.preventDefault();
    setErro(null);
    setEnviando(true);
    try {
      const { token } = await pedir('/api/v1/auth/login', LoginSchema, {
        method: 'POST',
        body: JSON.stringify({ email, senha }),
      });
      const s = sessao.entrar(token);
      if (!podeUsarOPainel(s.papel)) {
        sessao.sair();
        throw new Error('Esta central é para operadores. A família acompanha pelo app do Sombra.');
      }
      await navegar({ to: '/' });
    } catch (e) {
      setErro(e);
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className="topo grid min-h-dvh place-items-center px-4 py-10">
      <Folhagem />
      <form onSubmit={entrar} className="w-full max-w-md rounded-grande border border-borda bg-superficie p-6 shadow-[0_40px_80px_-40px_var(--folhagem)] sm:p-8">
        <div className="mb-6 flex items-center gap-3">
          <Marca tamanho={48} />
          <div>
            <h1 className="font-display text-2xl font-semibold">Central de cuidado</h1>
            <p className="text-texto-suave">Ninguém enfrenta o calor sozinho.</p>
          </div>
        </div>
        <label className="mb-4 block">
          <span className="mb-1 block font-semibold">E-mail</span>
          <input
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="min-h-12 w-full rounded-medio border border-borda bg-fundo px-3 text-base"
          />
        </label>
        <label className="mb-6 block">
          <span className="mb-1 block font-semibold">Senha</span>
          <input
            type="password"
            autoComplete="current-password"
            required
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
            className="min-h-12 w-full rounded-medio border border-borda bg-fundo px-3 text-base"
          />
        </label>
        {erro ? (
          <div className="mb-4">
            <ErroNaTela erro={erro} />
          </div>
        ) : null}
        <button
          type="submit"
          disabled={enviando}
          className="min-h-12 w-full rounded-medio bg-marca px-4 font-semibold text-sobre-marca hover:brightness-110 disabled:opacity-60"
        >
          {enviando ? 'Entrando…' : 'Entrar'}
        </button>
        <p className="mt-4 text-sm text-texto-suave">Aqui só aparece dado agregado e PII mascarada.</p>
      </form>
    </div>
  );
}
