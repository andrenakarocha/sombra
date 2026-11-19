-- V5 · a fonte dos pontos sem o identificador numérico do OpenStreetMap.
-- O "way/1135484967" tem a forma de um telefone fixo de São Paulo, (11) 3548-4967, e o motor de PII do Core (F2), que
-- confere toda resposta antes de ela sair, barra o texto: falso positivo num dado público, mas a guarda está certa em
-- desconfiar de dígitos soltos em texto livre. A fonte continua dizendo de onde veio o dado e o horário do OSM; o
-- identificador de cada ponto segue versionado em services/clima-service/scripts/pontos-osm.json.
-- Achado da validação da VM (infra/steps/90-validacao.sh, F8.6). A V4 não muda: migração aplicada é história.
UPDATE ponto_de_resfriamento
SET fonte = regexp_replace(fonte, ', (node|way|relation)/[0-9]+', '')
WHERE fonte ~ ', (node|way|relation)/[0-9]+';
