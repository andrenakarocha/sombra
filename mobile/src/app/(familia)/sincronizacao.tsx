import { RefreshControl, ScrollView, Switch, View } from 'react-native';

import type { Operacao } from '@/application/ports/outbox';
import { Botao } from '@/presentation/componentes/Botao';
import { Texto } from '@/presentation/componentes/Texto';
import { useSincronizacao } from '@/presentation/hooks/consultas';
import { useContainer } from '@/presentation/providers/container';
import { espaco, raio, toque, useTema } from '@/presentation/tema';
import { formatarHora } from '@/presentation/util/formato';

const DESCRICAO: Record<Operacao['tipo'], (op: Operacao) => string> = {
  CHECK_IN: (op) => (op.tipo === 'CHECK_IN' && op.payload.situacao === 'PRECISO_DE_AJUDA' ? 'Pedido de ajuda' : '"Estou bem"'),
  RECONHECER_ALERTA: (op) =>
    op.tipo === 'RECONHECER_ALERTA'
      ? { VOU_LIGAR: '"Vou ligar"', ESTOU_INDO: '"Estou indo"', ELA_ESTA_BEM: '"Ela está bem"' }[op.payload.acao]
      : '',
};

const SEGUNDO_PLANO = {
  ATIVO: 'Ligada: o celular envia sozinho de tempos em tempos, mesmo com o app fechado.',
  RESTRITO: 'O sistema não deixou (economia de bateria). O app envia quando estiver aberto.',
  INDISPONIVEL: 'Não disponível neste app de teste (Expo Go). O app envia quando estiver aberto.',
} as const;

export default function Sincronizacao() {
  const tema = useTema();
  const { central, barramento } = useContainer();
  const sinc = useSincronizacao();
  const atualizar = () => barramento.publicar({ recurso: 'sincronizacao' });

  const d = sinc.data;
  return (
    <ScrollView
      contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }}
      refreshControl={<RefreshControl refreshing={sinc.isRefetching} onRefresh={() => void sinc.refetch()} />}
    >
      <Texto>
        Tudo o que você faz no app vale na hora no celular e vai para o Sombra assim que der. Aqui fica o que ainda está
        esperando.
      </Texto>

      {d && (
        <>
          <Texto variante="subtitulo" forte accessibilityRole="header">
            {d.pendentes === 0 && d.deadLetter === 0 ? 'Tudo enviado.' : `${d.pendentes} esperando · ${d.deadLetter} não aceitos`}
          </Texto>

          {d.operacoes.map((op) => (
            <View
              key={op.operationId}
              accessible
              style={{ backgroundColor: tema.superficie, borderRadius: raio.medio, padding: espaco[4], gap: espaco[2] }}
            >
              <Texto forte>
                {DESCRICAO[op.tipo](op)} · {formatarHora(op.criadoEm)}
              </Texto>
              <Texto suave>
                {op.estado === 'DEAD_LETTER'
                  ? `Não aceito: ${op.ultimoErro ?? 'erro'}`
                  : op.tentativas === 0
                    ? 'Esperando a internet.'
                    : `Tentou ${op.tentativas} ${op.tentativas === 1 ? 'vez' : 'vezes'} (${op.ultimoErro}). Próxima às ${formatarHora(op.proximaTentativaEm)}.`}
              </Texto>
              {op.estado === 'DEAD_LETTER' && (
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: espaco[3] }}>
                  <Botao rotulo="Reenviar" dica="Tenta enviar este aviso de novo" aoTocar={() => central.reenviar(op.operationId).then(atualizar)} />
                  <Botao
                    rotulo="Descartar"
                    variante="secundario"
                    dica="Apaga este aviso do celular sem enviar"
                    aoTocar={() => central.descartar(op.operationId).then(atualizar)}
                  />
                </View>
              )}
            </View>
          ))}

          <Botao rotulo="Tentar enviar agora" dica="Envia agora o que está esperando a internet" aoTocar={() => central.tentarAgora().then(atualizar)} />

          <View
            style={{ flexDirection: 'row', alignItems: 'center', gap: espaco[3], minHeight: toque.minimo, marginTop: espaco[4] }}
          >
            <View style={{ flex: 1 }}>
              <Texto forte>Simular sem internet</Texto>
              <Texto variante="pequeno" suave>
                Para a demonstração: segura tudo no celular sem desligar o Wi-Fi.
              </Texto>
            </View>
            <Switch
              accessibilityLabel="Simular sem internet"
              accessibilityHint="Segura os envios no celular até você desligar"
              value={d.offlineForcado}
              onValueChange={(v) => {
                central.forcarOffline(v);
                atualizar();
              }}
              trackColor={{ true: tema.marca, false: tema.borda }}
            />
          </View>

          <Texto forte style={{ marginTop: espaco[4] }}>
            Envio com o app fechado
          </Texto>
          <Texto suave>{d.segundoPlano ? SEGUNDO_PLANO[d.segundoPlano] : 'Verificando…'}</Texto>
        </>
      )}
    </ScrollView>
  );
}
