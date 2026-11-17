// API de mentira para o modo demonstração: responde às mesmas rotas do Core com os dados de exemplo, em memória.
// Assim o professor abre o app sem subir o backend (spec 05, "Modo demonstração").

import { ErroDaApi } from '@/api/erros';
import type { Alerta, CheckIn, Domicilio, Leitura, NovoMorador } from '@/api/tipos';
import { exibirCartaoSus, exibirCpf, exibirTelefone } from '@/util/mascaras';

import { ALERTAS, CASAS, leituras24h, MORADORES, PONTOS, PRIVACIDADE, REDE } from './fixtures';

const MINUTO = 60 * 1000;

// Cópia profunda dos dados de exemplo (só JSON, sem datas nem funções).
const copia = <T>(v: T): T => JSON.parse(JSON.stringify(v));

function estadoInicial() {
  const agora = new Date();
  const casas: Domicilio[] = copia(CASAS).map((c) => ({
    ...c,
    ultimaLeitura: c.ultimaLeitura && { ...c.ultimaLeitura, momento: agora.toISOString() },
    ultimoCheckIn: new Date(agora.getTime() - 6 * 60 * MINUTO).toISOString(),
    alertaAberto: c.alertaAberto && { ...c.alertaAberto, prazo: new Date(agora.getTime() + 20 * MINUTO).toISOString() },
  }));
  const alertas: Alerta[] = copia(ALERTAS).map((a) =>
    a.estado === 'RESOLVIDO'
      ? a
      : {
          ...a,
          abertoEm: new Date(agora.getTime() - 10 * MINUTO).toISOString(),
          prazo: new Date(agora.getTime() + 20 * MINUTO).toISOString(),
          linhaDoTempo: a.linhaDoTempo?.map((e) => ({ ...e, momento: new Date(agora.getTime() - 10 * MINUTO).toISOString() })),
        },
  );
  const leituras = Object.fromEntries(casas.map((c) => [c.id, leituras24h(agora, c.ultimaLeitura!)]));
  return {
    casas,
    alertas,
    leituras,
    moradores: copia(MORADORES),
    privacidade: copia(PRIVACIDADE),
  };
}

let estado = estadoInicial();

export function reiniciarDemo() {
  estado = estadoInicial();
}

const pagina = <T>(itens: T[]) => ({ itens, pagina: 1, tamanho: itens.length, total: itens.length });

function casa(id: string) {
  const c = estado.casas.find((x) => x.id === id);
  if (!c) throw new ErroDaApi(404, 'Casa não encontrada', 'demo0000');
  return c;
}

function resolverAlertaDaCasa(c: Domicilio, descricao: string) {
  const alerta = estado.alertas.find((a) => a.id === c.alertaAberto?.id);
  if (alerta) {
    alerta.estado = 'RESOLVIDO';
    alerta.linhaDoTempo = [...(alerta.linhaDoTempo ?? []), { momento: new Date().toISOString(), degrau: alerta.degrau, descricao }];
  }
  c.alertaAberto = null;
}

export async function responderDemo<T>(metodo: string, caminho: string, corpo: unknown): Promise<T> {
  await new Promise((r) => setTimeout(r, 150));
  const [rota] = caminho.split('?');
  const partes = rota!.split('/').filter(Boolean);
  const [recurso, id, sub, subId] = partes;

  if (recurso === 'domicilios' && !id) return pagina(estado.casas) as T;
  if (recurso === 'domicilios' && id && !sub) return casa(id) as T;
  if (recurso === 'domicilios' && id && sub === 'leituras') {
    return pagina([...(estado.leituras[id] ?? [])].reverse()) as T;
  }
  if (recurso === 'domicilios' && id && sub === 'moradores') {
    if (metodo === 'GET') return (estado.moradores[id] ?? []) as T;
    const novo = corpo as Partial<NovoMorador>;
    const lista = (estado.moradores[id] ??= []);
    const atual = lista.find((m) => m.id === subId);
    const salvo = {
      id: atual?.id ?? `m-${Date.now()}`,
      nome: novo.nome ?? atual?.nome ?? '',
      idade: novo.idade ?? atual?.idade ?? 0,
      // Como o Core: a resposta volta mascarada.
      cpf: novo.cpf ? exibirCpf(novo.cpf) : (atual?.cpf ?? ''),
      cartaoSus: novo.cartaoSus ? exibirCartaoSus(novo.cartaoSus) : (atual?.cartaoSus ?? ''),
      telefone: novo.telefone ? exibirTelefone(novo.telefone) : (atual?.telefone ?? null),
      perfilDeSaude: novo.perfilDeSaude ? '[DADO DE SAÚDE PROTEGIDO]' : (atual?.perfilDeSaude ?? ''),
      fatoresDeRisco: novo.fatoresDeRisco ?? atual?.fatoresDeRisco ?? [],
    };
    if (atual) Object.assign(atual, salvo);
    else lista.push(salvo);
    return salvo as T;
  }
  if (recurso === 'domicilios' && id && sub === 'rede-de-cuidado') return (REDE[id] ?? []) as T;
  if (recurso === 'domicilios' && id && sub === 'pontos-de-resfriamento') return PONTOS as T;
  if (recurso === 'domicilios' && id && sub === 'privacidade') return estado.privacidade[id] as T;
  if (recurso === 'domicilios' && id && sub === 'check-ins') {
    const { situacao, momento } = corpo as CheckIn;
    const c = casa(id);
    c.ultimoCheckIn = momento;
    if (situacao === 'ESTOU_BEM') resolverAlertaDaCasa(c, 'Ela apertou "estou bem" no celular.');
    return undefined as T;
  }
  if (recurso === 'alertas' && !id) return pagina(estado.alertas) as T;
  if (recurso === 'alertas' && id && !sub) {
    const alerta = estado.alertas.find((a) => a.id === id);
    if (!alerta) throw new ErroDaApi(404, 'Alerta não encontrado', 'demo0000');
    return alerta as T;
  }
  if (recurso === 'alertas' && id && sub === 'reconhecer') {
    const { acao } = corpo as { acao: string };
    const alerta = estado.alertas.find((a) => a.id === id)!;
    if (acao === 'ELA_ESTA_BEM') resolverAlertaDaCasa(casa(alerta.domicilioId), 'A família confirmou que ela está bem.');
    else {
      alerta.estado = 'RECONHECIDO';
      const descricao = acao === 'VOU_LIGAR' ? 'Alguém da família vai ligar.' : 'Alguém da família está indo até a casa.';
      alerta.linhaDoTempo = [...(alerta.linhaDoTempo ?? []), { momento: new Date().toISOString(), degrau: alerta.degrau, descricao }];
    }
    return undefined as T;
  }
  throw new ErroDaApi(404, 'Não existe no modo demonstração', 'demo0000');
}

// A onda de calor do roteiro (§3.4 do plano), sem backend: a casa vai a PERIGO e abre um alerta.
export function simularOndaDeCalorNaDemo() {
  const c = estado.casas[0]!;
  const agora = new Date();
  const leitura: Leitura = { momento: agora.toISOString(), temperaturaC: 37.2, umidadePct: 45, indiceDeCalorC: 42.1 };
  c.nivelAtual = 'PERIGO';
  c.ultimaLeitura = leitura;
  estado.leituras[c.id] = [...(estado.leituras[c.id] ?? []).slice(1), leitura];
  const prazo = new Date(agora.getTime() + 15 * MINUTO).toISOString();
  const alerta: Alerta = {
    id: `demo-${agora.getTime()}`,
    domicilioId: c.id,
    apelidoDaCasa: c.apelido,
    tipo: 'DE_SENTINELA',
    estado: 'AGUARDANDO_CHECKIN',
    nivel: 'PERIGO',
    degrau: 0,
    abertoEm: agora.toISOString(),
    prazo,
    linhaDoTempo: [{ momento: agora.toISOString(), degrau: 0, descricao: 'Calor perigoso: a Sentinela apitou e pediu o "estou bem".' }],
  };
  estado.alertas = [alerta, ...estado.alertas.filter((a) => a.id !== c.alertaAberto?.id)];
  c.alertaAberto = { id: alerta.id, tipo: alerta.tipo, estado: alerta.estado, degrau: 0, prazo };
  return { domicilioId: c.id, alertaId: alerta.id, leitura, prazo };
}

export function escalonarNaDemo(alertaId: string) {
  const alerta = estado.alertas.find((a) => a.id === alertaId);
  if (!alerta || alerta.estado === 'RESOLVIDO') return null;
  alerta.estado = 'ESCALONADO';
  alerta.degrau = 1;
  const mensagem = 'A Dona Lurdes ainda não confirmou que está bem. Quer ligar?';
  alerta.linhaDoTempo = [...(alerta.linhaDoTempo ?? []), { momento: new Date().toISOString(), degrau: 1, descricao: 'Lembrete leve para a filha.' }];
  return { alertaId, domicilioId: alerta.domicilioId, degrau: 1, destinatario: 'FAMILIAR', mensagem };
}
