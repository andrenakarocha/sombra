// Adapters dos serviços do aparelho (Expo). Tudo que toca módulo nativo fica aqui, atrás das portas da aplicação.

import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import * as Crypto from 'expo-crypto';
import * as Network from 'expo-network';
import * as Notifications from 'expo-notifications';
import { AndroidImportance } from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

import type { GeradorDeId } from '@/application/ports/outbox';
import type {
  AgendadorDeLembretes,
  CanalDeTempoReal,
  CofreDeSegredos,
  CofreDeSessao,
  MonitorDeRede,
  Notificacao,
  Notificador,
} from '@/application/ports/servicos';
import { EVENTOS_DO_HUB } from '@/application/eventos/projetor';
import { proximaOcorrenciaEmSaoPaulo } from '@/domain/compartilhado/clock';

import { API_URL } from '../config';

// O token fica no Keychain / Keystore do sistema, nunca no SQLite.
const CHAVE_TOKEN = 'sombra.token';

export const cofreSecureStore: CofreDeSessao = {
  ler: () => SecureStore.getItemAsync(CHAVE_TOKEN),
  salvar: (token) => SecureStore.setItemAsync(CHAVE_TOKEN, token),
  apagar: () => SecureStore.deleteItemAsync(CHAVE_TOKEN),
};

export const idsExpoCrypto: GeradorDeId = { novo: () => Crypto.randomUUID() };

export const redeExpo: MonitorDeRede = {
  aoMudar(ouvinte) {
    const assinatura = Network.addNetworkStateListener((estado) => ouvinte(estado.isConnected ?? true));
    return () => assinatura.remove();
  },
};

export const notificadorExpo: Notificador = {
  async configurar() {
    Notifications.setNotificationHandler({
      handleNotification: async (n) => ({
        shouldShowBanner: true,
        shouldShowList: true,
        shouldPlaySound: n.request.content.data?.canal === 'alertas',
        shouldSetBadge: false,
      }),
    });
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('lembretes', { name: 'Lembretes', importance: AndroidImportance.DEFAULT });
      await Notifications.setNotificationChannelAsync('alertas', {
        name: 'Alertas de perigo',
        importance: AndroidImportance.MAX,
        vibrationPattern: [0, 400, 200, 400],
      });
    }
    const { status } = await Notifications.getPermissionsAsync();
    if (status !== 'granted') await Notifications.requestPermissionsAsync();
  },
  async notificar(n: Notificacao, umaPorVez: boolean) {
    if (umaPorVez) await Notifications.dismissAllNotificationsAsync();
    await Notifications.scheduleNotificationAsync({
      content: { title: n.titulo, body: n.corpo, data: { canal: n.canal } },
      trigger: { channelId: n.canal },
    });
  },
};

const NOVA_TENTATIVA_MS = 10_000;

// Hub SignalR do Core (docs/contratos/signalr.md). Sem hub, o app segue com REST e banco local e tenta de novo.
export const tempoRealSignalR: CanalDeTempoReal = {
  conectar(token, aoEvento, aoReconectar) {
    const conexao = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/sombra`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build();
    for (const evento of EVENTOS_DO_HUB) conexao.on(evento, (payload) => aoEvento(evento, payload));
    // Eventos perdidos não são reenviados: ao reconectar, busca tudo de novo.
    conexao.onreconnected(aoReconectar);
    let ativo = true;
    let espera: ReturnType<typeof setTimeout>;
    const conectar = () =>
      conexao.start().catch(() => {
        if (ativo) espera = setTimeout(conectar, NOVA_TENTATIVA_MS);
      });
    conectar();
    return () => {
      ativo = false;
      clearTimeout(espera);
      conexao.stop();
    };
  },
};

// Um item por Sentinela no Keychain / Keystore; a chave só aceita [A-Za-z0-9._-].
export const segredosSecureStore: CofreDeSegredos = {
  guardar: (deviceId, segredo) => SecureStore.setItemAsync(`sombra.sentinela.${deviceId}`, segredo),
  ler: (deviceId) => SecureStore.getItemAsync(`sombra.sentinela.${deviceId}`),
};

const PREFIXO_DA_ROTINA = 'sombra.rotina.';

// Check-in diário repete todo dia; água é só para hoje (depende do calor do dia). Os já passados não entram.
export const agendadorExpo: AgendadorDeLembretes = {
  async programar(lembretes, hoje) {
    const agendadas = await Notifications.getAllScheduledNotificationsAsync();
    for (const n of agendadas) {
      if (n.identifier.startsWith(PREFIXO_DA_ROTINA)) await Notifications.cancelScheduledNotificationAsync(n.identifier);
    }
    let programados = 0;
    for (const [i, l] of lembretes.entries()) {
      const [hora, minuto] = l.horario.split(':').map(Number) as [number, number];
      const { quando, jaPassouHoje } = proximaOcorrenciaEmSaoPaulo(hoje, hora, minuto);
      if (l.tipo === 'AGUA' && jaPassouHoje) continue;
      await Notifications.scheduleNotificationAsync({
        identifier: `${PREFIXO_DA_ROTINA}${i}`,
        content: { title: l.tipo === 'AGUA' ? 'Hora da água' : 'Bom dia', body: l.texto, data: { canal: 'lembretes' } },
        trigger:
          l.tipo === 'CHECK_IN_DIARIO'
            ? { type: Notifications.SchedulableTriggerInputTypes.DAILY, hour: hora, minute: minuto, channelId: 'lembretes' }
            : { type: Notifications.SchedulableTriggerInputTypes.DATE, date: quando, channelId: 'lembretes' },
      });
      programados++;
    }
    return programados;
  },
};
