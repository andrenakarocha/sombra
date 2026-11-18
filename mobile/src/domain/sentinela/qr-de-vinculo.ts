// O QR que a Sentinela mostra para ser vinculada a uma casa (proposta do app; a spec 04 precisa exibir este formato):
//   sombra://sentinela/vincular?v=1&d=snt-7f3a9c&c=K7P2QX
// `d` é o identificador público (docs/contratos/mqtt.asyncapi.yaml: `snt-` + 6 hex) e `c` um código de vínculo de uso
// único. Nada aqui é segredo: o segredo HMAC nasce no Core, no vínculo, e volta uma única vez.

import { falha, ok, type Result } from '../compartilhado/result';

export const ALFABETO_DO_CODIGO = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // sem 0/O e 1/I, que se confundem
const DEVICE_ID = /^snt-[0-9a-f]{6}$/;
const CODIGO = new RegExp(`^[${ALFABETO_DO_CODIGO}]{6}$`);

export type Vinculo = { deviceId: string; codigo: string };

export function lerQrDeVinculo(texto: string): Result<Vinculo> {
  const m = /^sombra:\/\/sentinela\/vincular\?(.*)$/.exec(texto.trim());
  if (!m) return falha('QR_DESCONHECIDO', 'Este QR não é de uma Sentinela do Sombra.');
  const parametros = new Map(m[1]!.split('&').map((par) => par.split('=') as [string, string]));
  if (parametros.get('v') !== '1') return falha('QR_VERSAO', 'Este QR é de outra versão da Sentinela. Atualize o app.');
  return vinculoDigitado(parametros.get('d') ?? '', parametros.get('c') ?? '');
}

// O mesmo vínculo digitado à mão, para quando a câmera não ajuda.
export function vinculoDigitado(deviceIdTexto: string, codigoTexto: string): Result<Vinculo> {
  const deviceId = deviceIdTexto.trim().toLowerCase();
  const codigo = codigoTexto.trim().toUpperCase();
  if (!DEVICE_ID.test(deviceId)) return falha('QR_DEVICE', 'O número da Sentinela tem o formato snt-7f3a9c.');
  if (!CODIGO.test(codigo)) return falha('QR_CODIGO', 'O código de vínculo tem 6 letras e números.');
  return ok({ deviceId, codigo });
}

// O segredo HMAC que o Core devolve no vínculo: 32 bytes em hexadecimal minúsculo (contrato MQTT).
export const segredoValido = (segredo: string) => /^[0-9a-f]{64}$/.test(segredo);
