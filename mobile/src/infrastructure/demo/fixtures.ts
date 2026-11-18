// Dados fictícios (personas do plano, nenhum dado pessoal real). As coordenadas dos pontos são de exemplo, perto da
// estação de Santana. Telefones no formato "(11) 90000-000X": a ligação
// não sai no modo demonstração.

import type {
  Alerta,
  ContatoDaRede,
  Domicilio,
  Leitura,
  Morador,
  PontoDeResfriamento,
  PrivacidadeDaCasa,
} from '@/application/modelos';

const LURDES = '0b6f3c1e-6c1d-4c55-9a51-2f3d1b9e7a10';
const ANTONIO = '7c2d9e4f-1a3b-4c5d-8e6f-0a1b2c3d4e5f';

export const CASAS: Domicilio[] = [
  {
    id: LURDES,
    apelido: 'Casa da Dona Lurdes',
    bairro: 'Santana',
    regiaoClimatica: 'SP-MIRANTE',
    nivelAtual: 'CAUTELA_EXTREMA',
    ultimaLeitura: { momento: '2026-11-18T18:00:00Z', temperaturaC: 33.4, umidadePct: 40, indiceDeCalorC: 34.4 },
    sentinela: { deviceId: 'snt-7f3a9c', online: true },
    ultimoCheckIn: '2026-11-18T12:02:00Z',
    alertaAberto: {
      id: '5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55',
      tipo: 'DE_SENTINELA',
      estado: 'AGUARDANDO_CHECKIN',
      degrau: 0,
      prazo: '2026-11-18T18:30:00Z',
    },
  },
  {
    id: ANTONIO,
    apelido: 'Casa do Seu Antônio',
    bairro: 'Tucuruvi',
    regiaoClimatica: 'SP-MIRANTE',
    nivelAtual: 'ATENCAO',
    ultimaLeitura: { momento: '2026-11-18T18:00:00Z', temperaturaC: 29.8, umidadePct: 52, indiceDeCalorC: 30.6 },
    sentinela: { deviceId: 'snt-2b8e1d', online: true },
    ultimoCheckIn: '2026-11-18T13:40:00Z',
    alertaAberto: null,
  },
];

export const ALERTAS: Alerta[] = [
  {
    id: '5d2e9a40-1b7c-4f0e-8e1a-3c9b2d7f6a55',
    domicilioId: LURDES,
    apelidoDaCasa: 'Casa da Dona Lurdes',
    tipo: 'DE_SENTINELA',
    estado: 'AGUARDANDO_CHECKIN',
    nivel: 'CAUTELA_EXTREMA',
    degrau: 0,
    abertoEm: '2026-11-18T18:00:00Z',
    prazo: '2026-11-18T18:30:00Z',
    linhaDoTempo: [{ momento: '2026-11-18T18:00:00Z', degrau: 0, descricao: 'A Sentinela pediu o "estou bem".' }],
  },
  {
    id: '9a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d',
    domicilioId: ANTONIO,
    apelidoDaCasa: 'Casa do Seu Antônio',
    tipo: 'PREVENTIVO',
    estado: 'RESOLVIDO',
    nivel: 'PERIGO',
    degrau: 0,
    abertoEm: '2026-11-17T19:00:00Z',
    prazo: '2026-11-17T19:30:00Z',
    linhaDoTempo: [{ momento: '2026-11-17T19:20:00Z', degrau: 0, descricao: 'Ele confirmou que está bem.' }],
  },
];

export const MORADORES: Record<string, Morador[]> = {
  [LURDES]: [
    {
      id: 'c1a8e2f4-7b3d-4e9a-8f2c-6d5b4a3e2f10',
      nome: 'Lurdes A.',
      idade: 78,
      telefone: '(11) 90000-0001',
      cpf: '***.982.247-**',
      cartaoSus: '*** **** **** 1234',
      perfilDeSaude: '[DADO DE SAÚDE PROTEGIDO]',
      fatoresDeRisco: ['USO_DE_DIURETICO'],
    },
  ],
  [ANTONIO]: [
    {
      id: 'd2b9f3a5-8c4e-4f0b-9a3d-7e6c5b4a3f21',
      nome: 'Antônio B.',
      idade: 84,
      telefone: '(11) 90000-0002',
      cpf: '***.613.508-**',
      cartaoSus: '*** **** **** 5678',
      perfilDeSaude: '[DADO DE SAÚDE PROTEGIDO]',
      fatoresDeRisco: ['IDADE_AVANCADA', 'CARDIOPATIA'],
    },
  ],
};

export const REDE: Record<string, ContatoDaRede[]> = {
  [LURDES]: [
    { id: 'r1', nome: 'Marta (filha)', papel: 'FAMILIAR', telefone: '(11) 90000-0003' },
    { id: 'r2', nome: 'Dona Zélia (vizinha)', papel: 'VIZINHO', telefone: '(11) 90000-0004' },
  ],
  [ANTONIO]: [{ id: 'r3', nome: 'Paulo (neto)', papel: 'FAMILIAR', telefone: '(11) 90000-0005' }],
};

export const PONTOS: PontoDeResfriamento[] = [
  {
    id: 'p1',
    nome: 'Biblioteca do bairro (exemplo)',
    tipo: 'BIBLIOTECA',
    distanciaKm: 0.8,
    horario: { abre: '09:00', fecha: '18:00' },
    acessivel: true,
    fonte: 'dados de exemplo',
    lat: -23.4935,
    lon: -46.6249,
  },
  {
    id: 'p2',
    nome: 'Centro de convivência do idoso (exemplo)',
    tipo: 'CENTRO_DE_CONVIVENCIA',
    distanciaKm: 1.6,
    horario: { abre: '08:00', fecha: '17:00' },
    acessivel: true,
    fonte: 'dados de exemplo',
    lat: -23.5071,
    lon: -46.6214,
  },
];

export const PRIVACIDADE: Record<string, PrivacidadeDaCasa> = {
  [LURDES]: {
    farol: 'AMARELO',
    mensagens: [
      {
        momento: '2026-11-18T17:40:00Z',
        mensagemCidadao:
          'Tentaram acessar o laudo de saúde da Dona Lurdes, mas o Sombra bloqueou para proteger a privacidade dela.',
        fonte: 'IA',
      },
    ],
  },
  [ANTONIO]: { farol: 'VERDE', mensagens: [] },
};

// Curva de um dia quente: mais fresco de madrugada, pico no meio da tarde, terminando no valor de agora.
export function leituras24h(agora: Date, final: Leitura): Leitura[] {
  const passos = 48;
  return Array.from({ length: passos }, (_, i) => {
    const momento = new Date(agora.getTime() - (passos - 1 - i) * 30 * 60 * 1000);
    const fase = Math.sin(((i - 10) / passos) * 2 * Math.PI);
    const indice = final.indiceDeCalorC - 6 + 6 * ((fase + 1) / 2) + (i / passos) * 2;
    return i === passos - 1
      ? { ...final, momento: momento.toISOString() }
      : {
          momento: momento.toISOString(),
          temperaturaC: Math.round((indice - 1) * 10) / 10,
          umidadePct: final.umidadePct,
          indiceDeCalorC: Math.round(indice * 10) / 10,
        };
  });
}
