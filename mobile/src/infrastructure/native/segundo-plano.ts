// Worker de sincronização em segundo plano (expo-background-task). O sistema decide quando roda (no mínimo a cada
// 15 min e sem garantia); no Expo Go do Android o TaskManager não existe, então o estado fica INDISPONIVEL e o app
// drena em primeiro plano, como sempre.

import * as BackgroundTask from 'expo-background-task';
import * as TaskManager from 'expo-task-manager';

import type { EstadoDoSegundoPlano, SincronizacaoEmSegundoPlano } from '@/application/ports/servicos';

export const TAREFA = 'sombra.sincronizar-outbox';

// O worker pode acordar com o app fechado: ele monta o próprio container (sem tela) e só drena a outbox.
export function definirTarefa(drenar: () => Promise<unknown>) {
  if (TaskManager.isTaskDefined(TAREFA)) return;
  TaskManager.defineTask(TAREFA, async () => {
    try {
      await drenar();
      return BackgroundTask.BackgroundTaskResult.Success;
    } catch {
      return BackgroundTask.BackgroundTaskResult.Failed;
    }
  });
}

export const workerExpo: SincronizacaoEmSegundoPlano = {
  async registrar(): Promise<EstadoDoSegundoPlano> {
    if (!(await TaskManager.isAvailableAsync())) return 'INDISPONIVEL';
    const status = await BackgroundTask.getStatusAsync();
    if (status !== BackgroundTask.BackgroundTaskStatus.Available) return 'RESTRITO';
    if (!(await TaskManager.isTaskRegisteredAsync(TAREFA))) {
      await BackgroundTask.registerTaskAsync(TAREFA, { minimumInterval: 15 });
    }
    return 'ATIVO';
  },
};
