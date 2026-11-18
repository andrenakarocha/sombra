import * as LocalAuthentication from 'expo-local-authentication';

import type { Biometria } from '@/application/ports/servicos';

export const biometriaExpo: Biometria = {
  async disponivel() {
    return (await LocalAuthentication.hasHardwareAsync()) && (await LocalAuthentication.isEnrolledAsync());
  },
  async autenticar(motivo) {
    const r = await LocalAuthentication.authenticateAsync({ promptMessage: motivo, cancelLabel: 'Agora não' });
    return r.success;
  },
};
