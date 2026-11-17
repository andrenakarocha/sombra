# Níveis de risco de calor

> **Contrato único** para a Sentinela, o Core, o Clima, o ML, o app e o painel. Se algo mudar aqui, muda em todos no
> mesmo PR.

## Índice de calor

- **Fórmula:** a equação oficial da NWS/WPC ([The Heat Index Equation](https://www.wpc.ncep.noaa.gov/html/heatindex_equation.shtml)),
  com a regressão de Rothfusz (1990, SR 90-23), os dois ajustes e a fórmula simples de Steadman.
- **Como o Sombra calcula** (a página oficial deixa uma ambiguidade, e esta é a leitura adotada por todas as linguagens):
  1. converte a temperatura para °F e calcula a fórmula simples;
  2. se a **média entre a fórmula simples e a temperatura** for ≥ 80 °F, usa a regressão de Rothfusz;
  3. aplica o ajuste de **umidade baixa** (UR < 13% e 80–112 °F, subtrai) ou de **umidade alta** (UR > 85% e 80–87 °F, soma);
  4. caso contrário, o índice é a própria fórmula simples;
  5. converte o resultado para °C.
- **Implementação de referência:** [`tools/indice-de-calor/vetores.py`](../../tools/indice-de-calor/vetores.py).
- **Vetores de teste:** [`indice-de-calor.vetores.json`](indice-de-calor.vetores.json), com 20 casos, tolerância de 0,05 °C,
  os cinco níveis e os dois ajustes. **Toda implementação (C#, Java, C++ e Python) roda esses vetores nos testes.**
  Para conferir: `tools/indice-de-calor/vetores.py`.

## Os cinco níveis

Faixas da tabela de índice de calor da NWS, convertidas de °F para °C com duas casas. A comparação é feita em °C, com
intervalo fechado embaixo e aberto em cima.

| Código | Índice de calor (°C) | NWS | Cor | Ícone | Texto para o cidadão |
|---|---|---|---|---|---|
| `NORMAL` | < 26,67 | < 80 °F | verde | ✓ | "Tudo bem por aqui." |
| `ATENCAO` | 26,67 a < 32,22 | Caution (80–90 °F) | amarelo | ! | "Está quente. Beba água." |
| `CAUTELA_EXTREMA` | 32,22 a < 39,44 | Extreme Caution (90–103 °F) | laranja | !! | "Calor forte. Fique na sombra e beba água." |
| `PERIGO` | 39,44 a < 51,67 | Danger (103–124 °F) | vermelho | ⚠ | "Calor perigoso. Aperte o botão verde se você está bem." |
| `PERIGO_EXTREMO` | ≥ 51,67 | Extreme Danger (≥ 125 °F) | vinho | ⛔ | "Calor extremo. Peça ajuda." |

Os valores exatos das cores saem dos tokens de design (F0.9). **Nenhuma tela comunica o nível só pela cor:** sempre
cor + ícone + texto.

## O que acontece em cada nível (D13)

Desenho que veio da entrevista: a Sentinela é **discreta** ("não pode ficar piscando toda hora") e a família **não recebe
alarme à toa** ("não queria um alerta de emergência toda vez que ela esquecesse de apertar").

| Nível | Sentinela | Pede "estou bem"? | Família |
|---|---|---|---|
| `NORMAL` | luz apagada, sem som | só o check-in diário, se ativado | nada |
| `ATENCAO` | luz apagada; **lembrete de água** no pico da tarde (15h–16h), no máximo 2 por dia | só o check-in diário, se ativado | nada |
| `CAUTELA_EXTREMA` | luz laranja **fixa**; 3 bipes ao entrar no nível | sim, prazo de 30 min | degrau 1 (lembrete leve) se o prazo vencer |
| `PERIGO` | luz vermelha **fixa**; 3 bipes, repetidos a cada 10 min | sim, prazo de 15 min | degraus 1 → 2 → 3 |
| `PERIGO_EXTREMO` | luz vermelha **fixa**; 3 bipes, repetidos a cada 5 min | sim, prazo de 15 min; o OLED sugere pedir ajuda | degraus 1 → 2 → 3 |

**Degraus do escalonamento:**
- 0 · a Sentinela lembra e explica o próximo passo;
- 1 · lembrete leve ao primeiro familiar;
- 2 · "quem está perto" (contato com papel `Vizinho`);
- 3 · alerta para toda a rede.

Os degraus 2 e 3 só acontecem com nível ≥ `PERIGO` ou com **pedido de ajuda**, que vai direto ao degrau 3 em qualquer
nível.

**À noite (22h–7h):** nada de lembrete de água, check-in diário ou bipes de `CAUTELA_EXTREMA`. `PERIGO` e
`PERIGO_EXTREMO` funcionam normalmente.

> Não confundir com o **farol de Risco Cidadão** (verde, amarelo e vermelho), que mede o risco de **privacidade** e é
> definido na spec 07.
