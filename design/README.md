# Direção de arte

> **Status:** proposta da F0.9, aguardando a aprovação do André. Abra a [`amostra.html`](amostra.html) no navegador
> (`?tema=claro` ou `?tema=escuro` força o tema).

## Conceito: luz filtrada pela folhagem

A identidade nasce do território da entrevista: **paredes cor de ocre, telhado de barro e a sombra recortada das
árvores na calçada**, que é o lugar mais fresco da rua às três da tarde.

- O **verde-petróleo da sombra** é a calma e a marca.
- A **telha** e o **ocre** são o calor.
- O **risco** sobe do verde ao vinho, sempre com cor, forma e palavra juntas.
- A textura de **sombra de folhas** (SVG, sem imagem) aparece só nos topos, e para quando o sistema pede menos
  movimento.

## Princípios

1. **Calma antes de alarme.** A Dona Cida pediu: nada piscando toda hora. Movimento só quando importa.
2. **Diga o que vem depois.** Toda tela que pede uma ação explica o que acontece se ninguém responder.
3. **Nunca só pela cor.** Cada nível tem cor, ícone de **forma** própria (círculo, triângulo, octógono) e uma frase
   curta. Contraste ≥ 7:1 no texto principal; alvos de toque de 48 px, e de 120 px no Modo Idoso.

## Arquivos

| Arquivo | O que é |
|---|---|
| [`tokens.json`](tokens.json) | **fonte da verdade**: primitivos (cor, fonte, tamanho, espaço, raio, toque, duração) e semânticos dos temas claro e escuro, com aliases `{cor.areia.50}` |
| [`tokens.css`](tokens.css) | gerado: variáveis CSS; o tema escuro entra por `prefers-color-scheme` ou por `data-tema="escuro"` |
| [`tokens.py`](tokens.py) | `gerar` escreve o CSS; `conferir` checa os 50 pares de contraste dos dois temas (WCAG 2.2) |
| [`amostra.html`](amostra.html) | página de amostra: níveis, cartão da casa, Modo Idoso, farol de privacidade, paleta e tipografia |

```bash
design/tokens.py gerar      # depois de editar o tokens.json
design/tokens.py conferir   # sai com 0 só se todos os pares passam
```

## Como usar

- **Use só os semânticos** (`--fundo`, `--superficie`, `--texto`, `--marca`, `--risco-perigo-fundo`…). Assim o tema escuro
  funciona sozinho. Os primitivos (`--cor-…`) ficam para casos muito específicos.
- **Painel** (F7.1): o `@theme` do Tailwind v4 aponta para essas variáveis.
- **App** (F6.1): o `tokens.py` ganha uma saída `theme.ts` com os mesmos nomes.
- **Pitch e PDFs** (F13.1, F12.3): o `kit.css` e os HTMLs das entregas importam o `tokens.css`.

## Tipografia

| Papel | Fonte | Por quê |
|---|---|---|
| Títulos | **Fraunces** (eixo `SOFT` no máximo) | serifa macia e acolhedora, com o calor de um bilhete escrito à mão |
| Texto | **Atkinson Hyperlegible Next** | desenhada para baixa visão: letras que não se confundem (I, l, 1 · O, 0) |
| Dados | **Atkinson Hyperlegible Mono** | IDs, medições e código, na mesma família |

Texto base de **18 px** (o comum é 16) e **28 px** no Modo Idoso. Na amostra, as fontes vêm do Google Fonts. No app e
no painel elas são empacotadas junto, e a licença de cada uma é conferida nesse momento.

## Marca

Uma casa (a sombra, verde-petróleo) sob um telhado de barro, com o sol ocre atrás. Está como `<symbol id="marca">` na
amostra e vira um SVG próprio quando a direção de arte for aprovada.
