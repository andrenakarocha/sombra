import { useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';

import { entrar } from '@/api/auth';
import { mensagemDeErro } from '@/api/mensagens';
import { Botao } from '@/componentes/Botao';
import { Campo } from '@/componentes/Campo';
import { Texto } from '@/componentes/Texto';
import { espaco, useTema } from '@/tema';

export default function Entrar() {
  const tema = useTema();
  const [email, setEmail] = useState('');
  const [senha, setSenha] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function aoEntrar() {
    setEnviando(true);
    setErro(null);
    try {
      await entrar(email.trim(), senha);
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
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}
