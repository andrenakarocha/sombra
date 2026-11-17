// Módulos nativos que o Jest não tem: SQLite, rede, armazenamento seguro e notificações.
jest.mock('expo-sqlite', () => ({ openDatabaseAsync: jest.fn() }));
jest.mock('expo-network', () => ({ addNetworkStateListener: () => ({ remove: () => {} }) }));
jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async () => null),
  setItemAsync: jest.fn(async () => {}),
  deleteItemAsync: jest.fn(async () => {}),
}));
jest.mock('expo-notifications', () => ({
  AndroidImportance: { DEFAULT: 3, MAX: 5 },
  setNotificationHandler: jest.fn(),
  setNotificationChannelAsync: jest.fn(async () => null),
  getPermissionsAsync: jest.fn(async () => ({ status: 'granted' })),
  requestPermissionsAsync: jest.fn(async () => ({ status: 'granted' })),
  dismissAllNotificationsAsync: jest.fn(async () => {}),
  scheduleNotificationAsync: jest.fn(async () => 'id'),
}));
