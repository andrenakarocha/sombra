// As implementações de verdade, no celular.

import { abrirBancoDoAparelho } from './database/expo-sqlite-database';
import { montarContainer } from './container';
import { ClienteHttp } from './http/cliente-http';
import { CoreHttpGateway } from './http/core-http-gateway';
import { cofreSecureStore, idsExpoCrypto, notificadorExpo, redeExpo, tempoRealSignalR } from './native/servicos-expo';

export async function criarContainerDoAparelho() {
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
  });
  aoSessaoVencida = () => container.sessao.sessaoVencida();
  return container;
}
