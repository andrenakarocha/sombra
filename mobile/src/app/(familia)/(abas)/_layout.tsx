import Ionicons from '@expo/vector-icons/Ionicons';
import { Tabs } from 'expo-router';
import type { ColorValue } from 'react-native';

import { useAlertas } from '@/api/consultas';
import { ativo } from '@/dominio/alerta';
import { fonte, useTema } from '@/tema';

type Icone = React.ComponentProps<typeof Ionicons>['name'];

// As cores das abas vêm dos tokens (texto hex), por isso o cast.
function icone(nome: Icone) {
  return function IconeDaAba({ color, size }: { color: ColorValue; size: number }) {
    return <Ionicons name={nome} color={color as string} size={size} />;
  };
}

export default function AbasLayout() {
  const tema = useTema();
  const ativos = useAlertas().data?.dados.filter(ativo).length ?? 0;
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: tema.marca,
        tabBarInactiveTintColor: tema.textoSuave,
        tabBarStyle: { backgroundColor: tema.superficie, borderTopColor: tema.borda, minHeight: 64 },
        tabBarLabelStyle: { fontFamily: fonte.textoForte, fontSize: 13 },
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Início', tabBarIcon: icone('home-outline') }} />
      <Tabs.Screen
        name="alertas"
        options={{
          title: 'Alertas',
          tabBarIcon: icone('notifications-outline'),
          tabBarBadge: ativos || undefined,
          tabBarAccessibilityLabel: ativos ? `Alertas, ${ativos} ativos` : 'Alertas',
        }}
      />
      <Tabs.Screen name="perto" options={{ title: 'Perto de mim', tabBarIcon: icone('location-outline') }} />
      <Tabs.Screen name="privacidade" options={{ title: 'Privacidade', tabBarIcon: icone('shield-checkmark-outline') }} />
      <Tabs.Screen name="ajustes" options={{ title: 'Ajustes', tabBarIcon: icone('settings-outline') }} />
    </Tabs>
  );
}
