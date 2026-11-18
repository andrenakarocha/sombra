// Regra de dependência das camadas, conferida em todo arquivo de src/ (inclusive imports relativos).
//   domain          → nada de fora (nem React, nem Expo, nem as outras camadas)
//   application     → domain (e o zustand sem React, para o estado da sessão)
//   infrastructure  → application, domain
//   presentation    → application, domain
//   app (rotas)     → presentation, application, domain; só o _layout monta a infraestrutura

import fs from 'node:fs';
import path from 'node:path';

const SRC = path.resolve(__dirname, '../src');

type Camada = 'domain' | 'application' | 'infrastructure' | 'presentation' | 'app';

function arquivos(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const caminho = path.join(dir, e.name);
    return e.isDirectory() ? arquivos(caminho) : /\.tsx?$/.test(e.name) ? [caminho] : [];
  });
}

const camadaDe = (arquivo: string) => path.relative(SRC, arquivo).split(path.sep)[0] as Camada;

// Para onde aponta cada import: outra camada de src/, ou um pacote (pelo nome).
function destinos(arquivo: string) {
  const codigo = fs.readFileSync(arquivo, 'utf8');
  const specs = [...codigo.matchAll(/(?:import|export)\s[^'"]*?from\s+'([^']+)'|require\('([^']+)'\)/g)].map((m) => m[1] ?? m[2]!);
  return specs.map((spec) => {
    if (spec.startsWith('@/')) return { spec, camada: spec.slice(2).split('/')[0] as Camada };
    if (spec.startsWith('.')) return { spec, camada: camadaDe(path.resolve(path.dirname(arquivo), spec)) };
    return { spec, pacote: spec };
  });
}

const PERMITIDO: Record<Camada, { camadas: Camada[]; pacote: (p: string) => boolean }> = {
  domain: { camadas: ['domain'], pacote: () => false },
  application: { camadas: ['application', 'domain'], pacote: (p) => p === 'zustand/vanilla' },
  infrastructure: { camadas: ['infrastructure', 'application', 'domain'], pacote: () => true },
  presentation: { camadas: ['presentation', 'application', 'domain'], pacote: () => true },
  app: { camadas: ['app', 'presentation', 'application', 'domain'], pacote: () => true },
};

const todos = arquivos(SRC);

describe('arquitetura em camadas', () => {
  it('encontra as cinco camadas', () => {
    expect(new Set(todos.map(camadaDe))).toEqual(new Set(['domain', 'application', 'infrastructure', 'presentation', 'app']));
  });

  it.each(Object.keys(PERMITIDO) as Camada[])('%s só depende do que pode', (camada) => {
    const violacoes = todos
      .filter((f) => camadaDe(f) === camada)
      .flatMap((f) =>
        destinos(f)
          .filter((d) => (d.pacote ? !PERMITIDO[camada].pacote(d.pacote) : !PERMITIDO[camada].camadas.includes(d.camada!)))
          .filter((d) => !(camada === 'app' && path.basename(f) === '_layout.tsx' && d.camada === 'infrastructure'))
          .map((d) => `${path.relative(SRC, f)} → ${d.spec}`),
      );
    expect(violacoes).toEqual([]);
  });

  it('só o _layout raiz conhece a infraestrutura', () => {
    const quem = todos
      .filter((f) => camadaDe(f) === 'app' && destinos(f).some((d) => d.camada === 'infrastructure'))
      .map((f) => path.relative(SRC, f));
    expect(quem).toEqual([path.join('app', '_layout.tsx')]);
  });
});
