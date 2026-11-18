import { z } from 'zod';

// Variáveis de ambiente validadas na carga: uma URL errada falha cedo, com mensagem, e não no primeiro fetch.
const Ambiente = z.object({
  EXPO_PUBLIC_API_URL: z.url().default('http://localhost:5080'),
});

// No celular, "localhost" é o próprio celular: use o IP do computador na rede (veja o README).
export const ambiente = Ambiente.parse({ EXPO_PUBLIC_API_URL: process.env.EXPO_PUBLIC_API_URL });

export const API_URL = ambiente.EXPO_PUBLIC_API_URL;
