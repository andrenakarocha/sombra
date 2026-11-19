-- V2 · pontos de resfriamento: lugares públicos e frescos para o pico do calor.

CREATE TABLE ponto_de_resfriamento (
    id        UUID             PRIMARY KEY,
    nome      VARCHAR(120)     NOT NULL,
    tipo      VARCHAR(32)      NOT NULL
        CHECK (tipo IN ('BIBLIOTECA', 'CENTRO_DE_CONVIVENCIA', 'UBS', 'CEU', 'SHOPPING', 'ESPACO_COMUNITARIO')),
    latitude  DOUBLE PRECISION NOT NULL CHECK (latitude BETWEEN -90 AND 90),
    longitude DOUBLE PRECISION NOT NULL CHECK (longitude BETWEEN -180 AND 180)
);

-- a busca por proximidade filtra antes por uma caixa de latitude e longitude
CREATE INDEX ix_ponto_de_resfriamento_lat_lon ON ponto_de_resfriamento (latitude, longitude);
