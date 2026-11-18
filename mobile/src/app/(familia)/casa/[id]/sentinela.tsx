import { CameraView, useCameraPermissions } from 'expo-camera';
import { useLocalSearchParams } from 'expo-router';
import { useRef, useState } from 'react';
import { ScrollView, View } from 'react-native';

import { lerQrDeVinculo, type Vinculo, vinculoDigitado } from '@/domain/sentinela/qr-de-vinculo';
import { Botao } from '@/presentation/componentes/Botao';
import { Campo } from '@/presentation/componentes/Campo';
import { Texto } from '@/presentation/componentes/Texto';
import { useContainer } from '@/presentation/providers/container';
import { espaco, fonte, raio, useTema } from '@/presentation/tema';
import { mensagemDeErro } from '@/presentation/util/mensagens';

export default function VincularSentinela() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const tema = useTema();
  const { casaESentinela } = useContainer();
  const [permissao, pedirPermissao] = useCameraPermissions();
  const [lendo, setLendo] = useState(false);
  const [deviceId, setDeviceId] = useState('');
  const [codigo, setCodigo] = useState('');
  const [mensagem, setMensagem] = useState<string | null>(null);
  const [segredo, setSegredo] = useState<string | null>(null);
  const ocupado = useRef(false);

  async function vincular(v: Vinculo) {
    if (ocupado.current) return;
    ocupado.current = true;
    setLendo(false);
    setMensagem('Vinculando…');
    try {
      const r = await casaESentinela.vincularSentinela(id, v);
      if (!r.ok) return setMensagem(r.erro.mensagem);
      setSegredo(r.valor.segredo);
      setMensagem(`Sentinela ${r.valor.deviceId} vinculada a esta casa.`);
    } catch (e) {
      setMensagem(mensagemDeErro(e));
    } finally {
      ocupado.current = false;
    }
  }

  function aoLerQr(texto: string) {
    const r = lerQrDeVinculo(texto);
    if (!r.ok) return setMensagem(r.erro.mensagem);
    void vincular(r.valor);
  }

  function aoDigitar() {
    const r = vinculoDigitado(deviceId, codigo);
    if (!r.ok) return setMensagem(r.erro.mensagem);
    void vincular(r.valor);
  }

  return (
    <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[4] }} keyboardShouldPersistTaps="handled">
      <Texto>Aponte a câmera para o QR que aparece na tela da Sentinela, ou digite o número e o código que estão nela.</Texto>

      {lendo && permissao?.granted ? (
        <View style={{ height: 280, borderRadius: raio.grande, overflow: 'hidden' }}>
          <CameraView
            style={{ flex: 1 }}
            facing="back"
            barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
            onBarcodeScanned={({ data }) => aoLerQr(data)}
            accessibilityLabel="Câmera lendo o QR da Sentinela"
          />
        </View>
      ) : (
        <Botao
          rotulo="Ler o QR da Sentinela"
          dica="Abre a câmera para ler o QR"
          aoTocar={async () => {
            if (!permissao?.granted && !(await pedirPermissao()).granted) {
              return setMensagem('Sem a câmera, digite o número e o código abaixo.');
            }
            setLendo(true);
          }}
        />
      )}

      <Campo rotulo="Número da Sentinela" value={deviceId} onChangeText={setDeviceId} autoCapitalize="none" placeholder="snt-7f3a9c" />
      <Campo rotulo="Código de vínculo" value={codigo} onChangeText={setCodigo} autoCapitalize="characters" placeholder="K7P2QX" />
      <Botao rotulo="Vincular com o número digitado" variante="secundario" aoTocar={aoDigitar} />

      {mensagem && <Texto accessibilityRole="alert">{mensagem}</Texto>}
      {segredo && (
        <View style={{ backgroundColor: tema.superficieAlternativa, borderRadius: raio.medio, padding: espaco[4], gap: espaco[2] }}>
          <Texto forte>Segredo da Sentinela (aparece só agora)</Texto>
          <Texto selectable style={{ fontFamily: fonte.mono, fontSize: 15 }}>
            {segredo}
          </Texto>
          <Texto variante="pequeno" suave>
            Ele assina as mensagens da Sentinela (HMAC). Fica guardado só no cofre do celular, não no aplicativo.
          </Texto>
        </View>
      )}
    </ScrollView>
  );
}
