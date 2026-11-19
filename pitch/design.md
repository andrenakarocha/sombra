# Sombra · identidade do pitch

Fonte: a direção de arte do Sombra ([`design/README.md`](../design/README.md), tokens em `design/tokens.css`). O vídeo usa
os mesmos tokens do app e do painel, no tema escuro, para que pitch e produto pareçam a mesma coisa.

## Conceito

**Luz filtrada pela folhagem.** O calor é a telha e o ocre; a calma é o verde-petróleo da sombra. O fundo é a noite da
casa, com a textura de sombra de folhas (SVG, `feTurbulence` com semente fixa) andando devagar. O risco sobe do verde ao
vinho, sempre com cor, forma e palavra.

## Paleta

| Papel | Token | Hex |
|---|---|---|
| Canvas | noite-950 | `#0C1617` |
| Superfície | noite-900 | `#122022` |
| Superfície elevada | noite-800 | `#1A2C2E` |
| Borda | noite-700 | `#25393B` |
| Texto secundário | noite-300 | `#B8C2C0` |
| Texto | areia-50 | `#FBF7EF` |
| Texto claro suave | areia-200 | `#E9DEC8` |
| Marca (sombra) | sombra-500 / 300 / 100 | `#1F7A73` / `#5FB3A8` / `#CFE8E3` |
| Sombra profunda | sombra-900 / 700 | `#0B3B39` / `#12524F` |
| Calor | telha-500 / 300 | `#B04F27` / `#E08A62` |
| Sol | ocre-500 / 300 | `#D9A441` / `#F0CF83` |
| Risco: normal | risco-verde-claro | `#5CC08F` |
| Risco: atenção | risco-amarelo-claro | `#F0C75A` |
| Risco: cautela | risco-laranja-claro | `#F59A4E` |
| Risco: perigo | risco-vermelho / claro | `#B8322A` / `#F0705F` |

## Tipografia

- **Display:** Fraunces 600–700. Voz de afirmação, editorial e quente.
- **Texto:** Atkinson Hyperlegible Next 300–700. A fonte do app, feita para baixa visão.
- **Dados:** Atkinson Hyperlegible Mono 400–700, caixa alta nos rótulos, `tabular-nums`.

## Motivos

- A marca: a casa com o sol (ocre), o telhado de telha e a porta.
- Os ícones de risco com forma própria: círculo, triângulo e octógono.
- Rótulos mono em caixa alta acima dos títulos.
- Calor como brilho radial telha e ocre; segurança como brilho verde-petróleo.

## Movimento

"Calma antes de alarme": entradas suaves com `power3.out` / `expo.out`, nada piscando, nenhuma mola forte
(`back.out(1.3)` no máximo). O único movimento contínuo é a folhagem e o brilho do fundo.

## Evitar

- Gradiente linear em tela cheia (banding em H.264). Usar brilho radial localizado.
- Cores fora da tabela acima.
- Emoji (as fontes embutidas não têm os glifos).
- Qualquer coisa piscando: foi o pedido da entrevista.
