// As implementações de verdade, no celular.

import { abrirBancoDoAparelho } from './database/expo-sqlite-database';
import { montarContainer } from './container';
import { ClienteHttp } from './http/cliente-http';
import { CoreHttpGateway } from './http/core-http-gateway';
import { biometriaExpo } from './native/biometria-expo';
import { localizacaoExpo } from './native/localizacao-expo';
import { definirTarefa, workerExpo } from './native/segundo-plano';
import {
  agendadorExpo,
  cofreSecureStore,
  idsExpoCrypto,
  notificadorExpo,
  redeExpo,
  segredosSecureStore,
  tempoRealSignalR,
} from './native/servicos-expo';

let unico: ReturnType<typeof montar> | null = null;

// Um container por processo: a tela e o worker de segundo plano usam o mesmo banco.
export function criarContainerDoAparelho() {
  unico ??= montar();
  return unico;
}

// A tarefa precisa ser definida no escopo do módulo, antes de qualquer tela (exigência do TaskManager).
definirTarefa(async () => (await criarContainerDoAparelho()).sincronizacao.motor.drenar());

async function montar() {
  const db = await abrirBancoDoAparelho();
  // O 401 encerra a sessão pelo caso de uso; a referência é preenchida logo abaixo.
  let aoSessaoVencida = async () => {};
  const http = new CoreHttpGateway(new ClienteHttp(() => aoSessaoVencida()));
  const container = await montarContainer({
    db,
    http,
    cofre: cofreSecureStore,
    notificador: notificadorExpo,
    tempoReal: tempoRealSignalR,
    rede: redeExpo,
    ids: idsExpoCrypto,
    worker: workerExpo,
    localizacao: localizacaoExpo,
    segredos: segredosSecureStore,
    agendador: agendadorExpo,
    biometria: biometriaExpo,
  });
  aoSessaoVencida = () => container.sessao.sessaoVencida();
  return container;
}
