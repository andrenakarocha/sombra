# Relatório de acertos do motor de PII

> Gerado por `make pii-relatorio` (teste `RelatorioDeAcertos` em `Sombra.Privacidade.Tests`). Regras: `pii-regras.json`.
> Os casos usam só dados sintéticos e o relatório não reproduz nenhum texto analisado.

**63 de 63 casos corretos** · falsos negativos: **0** · falsos positivos: **0**

## Por regra

| Regra | Casos com o dado | Detectados | Falsos negativos | Falsos positivos | Recall |
|---|---:|---:|---:|---:|---:|
| `cpf` | 8 | 8 | 0 | 0 | 100 % |
| `cns` | 3 | 3 | 0 | 0 | 100 % |
| `rg` | 3 | 3 | 0 | 0 | 100 % |
| `telefone` | 7 | 7 | 0 | 0 | 100 % |
| `email` | 4 | 4 | 0 | 0 | 100 % |
| `cep` | 2 | 2 | 0 | 0 | 100 % |
| `saude` | 7 | 7 | 0 | 0 | 100 % |
| `credencial` | 4 | 4 | 0 | 0 | 100 % |

## Por tipo de caso

| Tipo | Casos | Corretos |
|---|---:|---:|
| Positivo | 29 | 29 |
| Difícil | 12 | 12 |
| Negativo | 22 | 22 |

## Casos

| # | Tipo | Caso | Campo | Esperado | Obtido | |
|---:|---|---|---|---|---|---|
| 1 | Positivo | CPF pontuado no meio de uma frase | texto livre | `cpf` | `cpf` | ✅ |
| 2 | Positivo | CPF só com dígitos | texto livre | `cpf` | `cpf` | ✅ |
| 3 | Positivo | CPF colado em pontuação | texto livre | `cpf` | `cpf` | ✅ |
| 4 | Positivo | CPF no campo documento | `documento` | `cpf` | `cpf` | ✅ |
| 5 | Difícil | CPF digitado errado no campo cpf ainda é protegido | `cpfDoResponsavel` | `cpf` | `cpf` | ✅ |
| 6 | Negativo | Onze dígitos com verificador errado em texto livre | texto livre | nenhum | nenhum | ✅ |
| 7 | Negativo | Onze dígitos iguais | texto livre | nenhum | nenhum | ✅ |
| 8 | Difícil | CPF trocando um verificador | texto livre | nenhum | nenhum | ✅ |
| 9 | Positivo | Cartão SUS com espaços | texto livre | `cns` | `cns` | ✅ |
| 10 | Positivo | Cartão SUS só com dígitos | texto livre | `cns` | `cns` | ✅ |
| 11 | Positivo | Cartão SUS no campo cartaoSus | `cartaoSus` | `cns` | `cns` | ✅ |
| 12 | Negativo | Quinze dígitos que não passam na validação | texto livre | nenhum | nenhum | ✅ |
| 13 | Difícil | Número longo que contém quinze dígitos | texto livre | nenhum | nenhum | ✅ |
| 14 | Positivo | RG pontuado | texto livre | `rg` | `rg` | ✅ |
| 15 | Positivo | RG com dígito X | texto livre | `rg` | `rg` | ✅ |
| 16 | Positivo | RG sem pontuação no campo rg | `rg` | `rg` | `rg` | ✅ |
| 17 | Difícil | Campo organizacao não é RG | `organizacao` | nenhum | nenhum | ✅ |
| 18 | Negativo | Nove dígitos soltos sem campo | texto livre | nenhum | nenhum | ✅ |
| 19 | Positivo | Celular com DDD e hífen | texto livre | `telefone` | `telefone` | ✅ |
| 20 | Positivo | Celular com +55 | texto livre | `telefone` | `telefone` | ✅ |
| 21 | Positivo | Fixo com DDD | texto livre | `telefone` | `telefone` | ✅ |
| 22 | Positivo | Celular só com dígitos | texto livre | `telefone` | `telefone` | ✅ |
| 23 | Positivo | Telefone no campo celular | `celular` | `telefone` | `telefone` | ✅ |
| 24 | Difícil | Celular depois de emoji | texto livre | `telefone` | `telefone` | ✅ |
| 25 | Negativo | DDD inexistente | texto livre | nenhum | nenhum | ✅ |
| 26 | Negativo | Data e hora ISO 8601 | texto livre | nenhum | nenhum | ✅ |
| 27 | Positivo | E-mail em frase | texto livre | `email` | `email` | ✅ |
| 28 | Positivo | E-mail em maiúsculas | texto livre | `email` | `email` | ✅ |
| 29 | Difícil | E-mail com acento no usuário | texto livre | `email` | `email` | ✅ |
| 30 | Negativo | Arroba sem domínio | texto livre | nenhum | nenhum | ✅ |
| 31 | Positivo | CEP com hífen | texto livre | `cep` | `cep` | ✅ |
| 32 | Positivo | CEP sem hífen no campo cep | `cep` | `cep` | `cep` | ✅ |
| 33 | Negativo | Oito dígitos soltos | texto livre | nenhum | nenhum | ✅ |
| 34 | Positivo | Campo laudo com texto comum | `laudo` | `saude` | `saude` | ✅ |
| 35 | Positivo | Campo do perfil de saúde | `perfilDeSaude` | `saude` | `saude` | ✅ |
| 36 | Positivo | CID-10 no meio de uma frase | texto livre | `saude` | `saude` | ✅ |
| 37 | Positivo | CID-10 com subcategoria | texto livre | `saude` | `saude` | ✅ |
| 38 | Positivo | Remédios do dicionário | texto livre | `saude` | `saude` | ✅ |
| 39 | Difícil | Condição em maiúsculas e sem acento | texto livre | `saude` | `saude` | ✅ |
| 40 | Difícil | Termo de saúde decomposto em NFD | texto livre | `saude` | `saude` | ✅ |
| 41 | Difícil | Campo cidade não é CID | `cidade` | nenhum | nenhum | ✅ |
| 42 | Negativo | Palavra que contém CID | texto livre | nenhum | nenhum | ✅ |
| 43 | Negativo | Índice de calor não é dado de saúde | texto livre | nenhum | nenhum | ✅ |
| 44 | Positivo | Campo senha | `senha` | `credencial` | `credencial` | ✅ |
| 45 | Positivo | JWT em texto | texto livre | `credencial` | `credencial` | ✅ |
| 46 | Positivo | Cabeçalho Bearer | texto livre | `credencial` | `credencial` | ✅ |
| 47 | Positivo | Chave de API de alta entropia | texto livre | `credencial` | `credencial` | ✅ |
| 48 | Negativo | UUID de correlação | texto livre | nenhum | nenhum | ✅ |
| 49 | Negativo | Identificador longo sem dígitos | texto livre | nenhum | nenhum | ✅ |
| 50 | Negativo | Hash curto de commit | texto livre | nenhum | nenhum | ✅ |
| 51 | Difícil | Assinatura em base64 no campo sig não é segredo | `sig` | nenhum | nenhum | ✅ |
| 52 | Negativo | Identificador hex (GUID sem hífens) não é segredo | texto livre | nenhum | nenhum | ✅ |
| 53 | Negativo | Hash SHA-256 em hex não é segredo | texto livre | nenhum | nenhum | ✅ |
| 54 | Negativo | CPF válido colado em letras, dentro de um hex | texto livre | nenhum | nenhum | ✅ |
| 55 | Negativo | Celular colado em letras, dentro de um token | texto livre | nenhum | nenhum | ✅ |
| 56 | Negativo | Tópico MQTT com prefixo de teste | texto livre | nenhum | nenhum | ✅ |
| 57 | Difícil | CPF escondido no campo sig continua sendo CPF | `sig` | `cpf` | `cpf` | ✅ |
| 58 | Positivo | Vários dados no mesmo texto livre | texto livre | `telefone`, `email`, `cpf` | `cpf`, `email`, `telefone` | ✅ |
| 59 | Difícil | JSON serializado dentro de um campo | texto livre | `cpf` | `cpf` | ✅ |
| 60 | Negativo | Coordenadas | texto livre | nenhum | nenhum | ✅ |
| 61 | Negativo | Mensagem de alerta comum | texto livre | nenhum | nenhum | ✅ |
| 62 | Negativo | Identificador da Sentinela | texto livre | nenhum | nenhum | ✅ |
| 63 | Negativo | Campo vazio com chave sensível | `cpf` | nenhum | nenhum | ✅ |
