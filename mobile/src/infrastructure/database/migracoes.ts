// Migrações versionadas pelo PRAGMA user_version: a posição na lista é a versão. Nunca edite uma que já saiu;
// acrescente outra. As 1 a 3 são da primeira versão do app (cache em JSON) e ficam para quem já tem o banco.

import type { SqlDatabase } from './sql-database';

export const MIGRACOES: readonly string[] = [
  // 1 · cache das casas
  `CREATE TABLE casas (id TEXT PRIMARY KEY NOT NULL, dados TEXT NOT NULL, atualizado_em TEXT NOT NULL);`,
  // 2 · cache dos alertas
  `CREATE TABLE alertas (id TEXT PRIMARY KEY NOT NULL, domicilio_id TEXT NOT NULL, dados TEXT NOT NULL, atualizado_em TEXT NOT NULL);
   CREATE INDEX alertas_domicilio ON alertas (domicilio_id);`,
  // 3 · fila offline de check-ins e preferências do aparelho
  `CREATE TABLE fila_checkins (
     id INTEGER PRIMARY KEY AUTOINCREMENT,
     domicilio_id TEXT NOT NULL,
     situacao TEXT NOT NULL CHECK (situacao IN ('ESTOU_BEM', 'PRECISO_DE_AJUDA')),
     momento TEXT NOT NULL
   );
   CREATE TABLE preferencias (chave TEXT PRIMARY KEY NOT NULL, valor TEXT NOT NULL);`,
  // 4 · local-first: o SQLite vira a fonte da verdade das telas, com tabelas normalizadas e uma outbox única.
  //     Os check-ins que estavam na fila antiga entram na outbox; o cache em JSON é descartado (volta do servidor).
  `CREATE TABLE casas_v4 (
     id TEXT PRIMARY KEY NOT NULL,
     apelido TEXT NOT NULL,
     bairro TEXT NOT NULL,
     regiao_climatica TEXT NOT NULL,
     nivel_atual TEXT NOT NULL,
     sentinela_device_id TEXT,
     sentinela_online INTEGER,
     ultimo_checkin TEXT,
     alerta_aberto_id TEXT,
     -- resumo do alerta como o servidor mandou; o alerta completo (tabela alertas) tem prioridade quando existe
     alerta_tipo TEXT,
     alerta_estado TEXT,
     alerta_degrau INTEGER,
     alerta_prazo TEXT,
     atualizado_em TEXT NOT NULL
   );
   CREATE TABLE leituras (
     domicilio_id TEXT NOT NULL REFERENCES casas_v4 (id) ON DELETE CASCADE,
     momento TEXT NOT NULL,
     temperatura_c REAL NOT NULL,
     umidade_pct REAL NOT NULL,
     indice_de_calor_c REAL NOT NULL,
     PRIMARY KEY (domicilio_id, momento)
   );
   CREATE TABLE alertas_v4 (
     id TEXT PRIMARY KEY NOT NULL,
     domicilio_id TEXT NOT NULL,
     apelido_da_casa TEXT NOT NULL,
     tipo TEXT NOT NULL CHECK (tipo IN ('PREVENTIVO', 'DE_SENTINELA')),
     estado TEXT NOT NULL CHECK (estado IN ('AGUARDANDO_CHECKIN', 'ESCALONADO', 'RECONHECIDO', 'RESOLVIDO')),
     nivel TEXT NOT NULL,
     degrau INTEGER NOT NULL CHECK (degrau BETWEEN 0 AND 3),
     aberto_em TEXT NOT NULL,
     prazo TEXT NOT NULL,
     pedido_de_ajuda INTEGER NOT NULL DEFAULT 0,
     tem_vizinho INTEGER NOT NULL DEFAULT 1,
     resolvido_por TEXT
   );
   CREATE INDEX alertas_v4_domicilio ON alertas_v4 (domicilio_id, estado);
   CREATE TABLE eventos_do_alerta (
     alerta_id TEXT NOT NULL REFERENCES alertas_v4 (id) ON DELETE CASCADE,
     ordem INTEGER NOT NULL,
     momento TEXT NOT NULL,
     degrau INTEGER NOT NULL,
     descricao TEXT NOT NULL,
     PRIMARY KEY (alerta_id, ordem)
   );
   CREATE TABLE moradores (
     id TEXT PRIMARY KEY NOT NULL,
     domicilio_id TEXT NOT NULL REFERENCES casas_v4 (id) ON DELETE CASCADE,
     nome TEXT NOT NULL,
     idade INTEGER NOT NULL,
     telefone TEXT,
     cpf_mascarado TEXT NOT NULL,
     cartao_sus_mascarado TEXT NOT NULL,
     perfil_de_saude TEXT NOT NULL,
     fatores_de_risco TEXT NOT NULL
   );
   CREATE TABLE rede_de_cuidado (
     domicilio_id TEXT NOT NULL REFERENCES casas_v4 (id) ON DELETE CASCADE,
     ordem INTEGER NOT NULL,
     id TEXT NOT NULL,
     nome TEXT NOT NULL,
     papel TEXT NOT NULL CHECK (papel IN ('FAMILIAR', 'VIZINHO')),
     telefone TEXT NOT NULL,
     PRIMARY KEY (domicilio_id, ordem)
   );
   CREATE TABLE outbox (
     operation_id TEXT PRIMARY KEY NOT NULL,
     tipo TEXT NOT NULL,
     payload TEXT NOT NULL,
     criado_em TEXT NOT NULL,
     tentativas INTEGER NOT NULL DEFAULT 0,
     proxima_tentativa_em TEXT NOT NULL,
     estado TEXT NOT NULL CHECK (estado IN ('PENDENTE', 'ENVIADO', 'DEAD_LETTER')),
     ultimo_erro TEXT
   );
   CREATE INDEX outbox_fila ON outbox (estado, proxima_tentativa_em);
   CREATE TABLE sincronizacao (recurso TEXT PRIMARY KEY NOT NULL, atualizado_em TEXT NOT NULL);
   INSERT INTO outbox (operation_id, tipo, payload, criado_em, proxima_tentativa_em, estado)
     SELECT 'migrado-' || id, 'CHECK_IN',
            json_object('domicilioId', domicilio_id, 'situacao', situacao, 'momento', momento),
            momento, momento, 'PENDENTE'
     FROM fila_checkins;
   DROP TABLE fila_checkins;
   DROP TABLE casas;
   DROP TABLE alertas;
   ALTER TABLE casas_v4 RENAME TO casas;
   ALTER TABLE alertas_v4 RENAME TO alertas;`,
];

export async function migrar(db: SqlDatabase) {
  const linha = await db.first<{ user_version: number }>('PRAGMA user_version');
  for (let versao = linha?.user_version ?? 0; versao < MIGRACOES.length; versao++) {
    await db.transaction(async () => {
      await db.exec(MIGRACOES[versao]!);
      await db.exec(`PRAGMA user_version = ${versao + 1}`);
    });
  }
}
