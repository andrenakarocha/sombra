// Sombra · Sentinela · escuta o hub do SignalR como o painel escuta (F9.5): entra como operador, assina os eventos do
// contrato e imprime uma linha JSON por evento com o correlationId, até receber SIGTERM ou passar o tempo pedido.
// Usado por integrar-com-core.sh para provar que o ID da mensagem MQTT chega igual ao painel.
//   SENHA=… node iot/sentinela/testes/ouvir-hub.mjs [segundos]
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), '../../..');
const { HubConnectionBuilder, LogLevel } = createRequire(join(RAIZ, 'web/painel/package.json'))('@microsoft/signalr');
const CORE = process.env.CORE_URL ?? 'http://localhost:5080';
const EVENTOS = ['LeituraRegistrada', 'SentinelaStatus', 'AlertaAberto', 'AlertaEscalonado', 'AlertaResolvido', 'CheckInRegistrado', 'RiscoCidadaoAtualizado'];

const resposta = await fetch(`${CORE}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ email: 'operador@demo.sombra', senha: process.env.SENHA ?? '' }),
});
if (!resposta.ok) throw new Error(`login do operador falhou: HTTP ${resposta.status}`);
const { token } = await resposta.json();

const hub = new HubConnectionBuilder()
  .withUrl(`${CORE}/hubs/sombra`, { accessTokenFactory: () => token })
  .configureLogging(LogLevel.Warning)
  .build();
for (const nome of EVENTOS) {
  hub.on(nome, (evento) => console.log(JSON.stringify({ evento: nome, correlationId: evento?.correlationId ?? null, ...evento })));
}
await hub.start();
console.error('hub.conectado');

const fim = async () => {
  await hub.stop();
  process.exit(0);
};
process.on('SIGTERM', fim);
setTimeout(fim, Number(process.argv[2] ?? 60) * 1000);
