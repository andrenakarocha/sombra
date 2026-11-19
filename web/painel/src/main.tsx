import { QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { consultas } from './app/consultas';
import { roteador } from './app/rotas';
import './estilo.css';

createRoot(document.getElementById('raiz')!).render(
  <StrictMode>
    <QueryClientProvider client={consultas}>
      <RouterProvider router={roteador} />
    </QueryClientProvider>
  </StrictMode>,
);
