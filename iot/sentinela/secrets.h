// Sombra · Sentinela · segredos DE DEMONSTRAÇÃO, públicos de propósito (o projeto do Wokwi é aberto).
// O segredo HMAC é o dos vetores do contrato (docs/contratos/mqtt-assinatura.vetores.json): para a demo no Wokwi, o Core
// usa o mesmo em Demo__SentinelaSegredoHex. Uma Sentinela real recebe o próprio segredo no vínculo e nunca o publica.
#pragma once
#define WIFI_SSID "Wokwi-GUEST"
#define WIFI_SENHA ""
#define DEVICE_ID "snt-7f3a9c"
#define SEGREDO_HEX "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"
// O código de vínculo vem impresso na etiqueta da Sentinela: é o HMAC do deviceId com a chave de vínculo do servidor,
// que o firmware nunca tem. Este é o do vetor de contrato (docs/contratos/vinculo-da-sentinela.vetores.json, chave de
// teste 2021…3f): para a demo no Wokwi, o Core usa a mesma em Sentinela__ChaveDeVinculo.
#define CODIGO_DE_VINCULO "3NGMZ2"
