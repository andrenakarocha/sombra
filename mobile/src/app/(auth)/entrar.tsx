import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { mensagemDeErro } from '@/presentation/util/mensagens';
import { Botao } from '@/presentation/componentes/Botao';
import { Campo } from '@/presentation/componentes/Campo';
import { Texto } from '@/presentation/componentes/Texto';
import { useContainer } from '@/presentation/providers/container';
import { espaco, useTema } from '@/presentation/tema';

export default function Entrar() {
  const tema = useTema();
  const { sessao } = useContainer();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function aoEntrar() {
    setEnviando(true);
    setErro(null);
    try {
      await sessao.entrar(email, senha);
    } catch (e) {
      setErro(mensagemDeErro(e, { 401: 'E-mail ou senha incorretos.' }));
    } finally {
      setEnviando(false);
    }
  }

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: tema.fundo }}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView contentContainerStyle={{ padding: espaco[5], gap: espaco[5] }} keyboardShouldPersistTaps="handled">
          <Texto variante="tituloGrande" accessibilityRole="header">
            Sombra
          </Texto>
          <Texto suave>Ninguém enfrenta o calor sozinho.</Texto>
          <Campo
            rotulo="E-mail"
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoComplete="email"
            keyboardType="email-address"
            textContentType="emailAddress"
          />
          <Campo
            rotulo="Senha"
            value={senha}
            onChangeText={setSenha}
            secureTextEntry
            autoComplete="current-password"
            textContentType="password"
            onSubmitEditing={aoEntrar}
          />
          {erro && (
            <Texto accessibilityRole="alert" accessibilityLiveRegion="polite" style={{ color: tema.destaque }}>
              {erro}
            </Texto>
          )}
          <Botao rotulo={enviando ? 'Entrando…' : 'Entrar'} aoTocar={aoEntrar} />
          <Texto suave>Sem servidor por perto? Dá para conhecer o app com dados de exemplo.</Texto>
          <Botao
            rotulo="Ver com dados de exemplo"
            variante="secundario"
            dica="Abre o app no modo demonstração, sem conta e sem servidor"
            aoTocar={() => sessao.entrarNaDemonstracao()}
          />
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
