import { QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import { consultas } from '@/app/consultas';
import { simularEvento } from '@/api/tempo-real';
import { VisaoGeral } from '@/paginas/VisaoGeral';

const resumo = (alertasAtivos: number, mediana: number | null) => ({
  casasMonitoradas: 2,
  alertasAtivos,
  tempoMedianoAteCheckInMin: mediana,
  alertasResolvidosSemUltimoDegrauPct: 91.7,
  alarmesDesnecessariosSemana: 0,
  escalonamentosHojePorDegrau: { '1': 1, '2': 0, '3': 0 },
  sentinelasOffline: 0,
  alertasAtivosPorNivel: alertasAtivos ? { PERIGO: alertasAtivos } : {},
  alertasDaSemanaPorBairro: { Santana: 1 },
});

const casa = {
  id: 'c1',
  apelido: 'Casa da Dona Lurdes',
  bairro: 'Santana',
  regiaoClimatica: 'SP-MIRANTE',
  monitorado: true,
  nivelAtual: 'PERIGO',
  ultimaLeitura: null,
  sentinela: { deviceId: 'snt-7f3a9c', online: true },
  ultimoCheckIn: null,
  alertaAberto: null,
};

describe('visão geral ao vivo', () => {
  let atual = resumo(1, null);

  beforeEach(() => {
    consultas.clear();
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const corpo = url.includes('/painel/resumo') ? atual : { itens: [casa], pagina: 1, tamanho: 100, total: 1 };
        return new Response(JSON.stringify(corpo), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }),
    );
  });

  afterEach(() => vi.unstubAllGlobals());

  it('mostra o indicador de impacto e muda sozinha quando o hub avisa que um alerta foi resolvido', async () => {
    render(
      <QueryClientProvider client={consultas}>
        <VisaoGeral />
      </QueryClientProvider>,
    );

    expect(await screen.findByText('Ainda sem alerta resolvido')).toBeInTheDocument();
    expect(screen.getByText('Casa da Dona Lurdes')).toBeInTheDocument();

    atual = resumo(0, 6.5);
    act(() => simularEvento('AlertaResolvido', { alertaId: 'a1', domicilioId: 'c1', minutosAteCheckIn: 6.5 }));

    expect(await screen.findByText('6,5 min')).toBeInTheDocument();
    expect(screen.getByText('Dentro da meta de 15 min')).toBeInTheDocument();
    expect(screen.getByText('Alerta resolvido: "estou bem" em 6,5 min.')).toBeInTheDocument();
    expect(screen.getAllByText('Casa da Dona Lurdes:').length).toBeGreaterThan(0);
    expect(screen.getByText('Nenhum alerta ativo agora.')).toBeInTheDocument();
  });

  it('o nível aparece com palavra e forma, não só com cor', async () => {
    render(
      <QueryClientProvider client={consultas}>
        <VisaoGeral />
      </QueryClientProvider>,
    );
    const selos = await screen.findAllByText('Perigo');
    expect(selos[0]?.closest('span')?.querySelector('svg')).not.toBeNull();
  });
});
