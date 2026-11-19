-- V3 · evolução do esquema (nunca se edita uma migração aplicada): o ponto de resfriamento passa a dizer quando abre,
-- se é acessível e de onde veio o dado. Feita como se a tabela já tivesse linhas: colunas nulas, preenchimento e só
-- então NOT NULL e as restrições.

ALTER TABLE ponto_de_resfriamento
    ADD COLUMN acessivel BOOLEAN,
    ADD COLUMN abre      TIME,
    ADD COLUMN fecha     TIME,
    ADD COLUMN fonte     VARCHAR(200);

UPDATE ponto_de_resfriamento
SET acessivel = COALESCE(acessivel, FALSE),
    abre      = COALESCE(abre, TIME '09:00'),
    fecha     = COALESCE(fecha, TIME '18:00'),
    fonte     = COALESCE(fonte, 'cadastro anterior à V3, sem fonte registrada');

ALTER TABLE ponto_de_resfriamento
    ALTER COLUMN acessivel SET NOT NULL,
    ALTER COLUMN acessivel SET DEFAULT FALSE,
    ALTER COLUMN abre SET NOT NULL,
    ALTER COLUMN fecha SET NOT NULL,
    ALTER COLUMN fonte SET NOT NULL,
    ADD CONSTRAINT ck_ponto_abre_antes_de_fechar CHECK (abre < fecha);
