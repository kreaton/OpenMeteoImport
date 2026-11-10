-- OpenMeteoImport weather schema for MariaDB / MySQL
-- Assumes the default Output:TableName (weather_hourly); adjust if you changed it.
-- Run against an existing database:  mysql -u root -p weather < schema.sql

SET NAMES utf8mb4;

-- ---------------------------------------------------------------------------
-- Main data table (also created by the console app; safe to run both)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS weather_hourly (
    location             VARCHAR(100) NOT NULL,
    time                 DATETIME NOT NULL,
    latitude             DOUBLE NOT NULL,
    longitude            DOUBLE NOT NULL,
    temperature_2m       DOUBLE NULL,
    relative_humidity_2m DOUBLE NULL,
    precipitation        DOUBLE NULL,
    pressure_msl         DOUBLE NULL,
    wind_speed_10m       DOUBLE NULL,
    wind_direction_10m   DOUBLE NULL,
    cloud_cover          DOUBLE NULL,
    PRIMARY KEY (location, time)
)CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- Cloud cover (%)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cloudcover (
    id      TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    min_pct TINYINT UNSIGNED NOT NULL,  -- inclusive
    max_pct TINYINT UNSIGNED NOT NULL,  -- inclusive
    text_en VARCHAR(50) NOT NULL,
    text_da VARCHAR(50) NOT NULL,
    UNIQUE KEY (min_pct, max_pct)
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

INSERT INTO cloudcover (id, min_pct, max_pct, text_en, text_da) VALUES
(1,   0,  10, 'Clear',         'Klar himmel'),
(2,  11,  25, 'Mostly clear',  'Overvejende klart'),
(3,  26,  50, 'Partly cloudy', 'Delvist skyet'),
(4,  51,  75, 'Mostly cloudy', 'Overvejende skyet'),
(5,  76,  90, 'Cloudy',        'Skyet'),
(6,  91, 100, 'Overcast',      'Overskyet')
ON DUPLICATE KEY UPDATE text_en = VALUES(text_en), text_da = VALUES(text_da);

-- ---------------------------------------------------------------------------
-- Wind speed (Beaufort, km/h)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS windspeed (
    beaufort TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    min_kmh  DECIMAL(5,1) NOT NULL,  -- inclusive
    max_kmh  DECIMAL(5,1) NOT NULL,  -- exclusive
    text_en  VARCHAR(50) NOT NULL,
    text_da  VARCHAR(50) NOT NULL
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

INSERT INTO windspeed (beaufort, min_kmh, max_kmh, text_en, text_da) VALUES
(0,    0,    1, 'Calm',            'Stille'),
(1,    1,    6, 'Light air',       'Næsten stille'),
(2,    6,   12, 'Light breeze',    'Svag vind'),
(3,   12,   20, 'Gentle breeze',   'Let vind'),
(4,   20,   29, 'Moderate breeze', 'Jævn vind'),
(5,   29,   39, 'Fresh breeze',    'Frisk vind'),
(6,   39,   50, 'Strong breeze',   'Hård vind'),
(7,   50,   62, 'Near gale',       'Stiv kuling'),
(8,   62,   75, 'Gale',            'Hård kuling'),
(9,   75,   89, 'Strong gale',     'Stormende kuling'),
(10,  89,  103, 'Storm',           'Storm'),
(11, 103,  118, 'Violent storm',   'Stærk storm'),
(12, 118, 9999, 'Hurricane',       'Orkan')
ON DUPLICATE KEY UPDATE text_en = VALUES(text_en), text_da = VALUES(text_da);

-- ---------------------------------------------------------------------------
-- Wind direction (16 compass points, 22.5 degrees each)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS winddirection (
    id       TINYINT UNSIGNED NOT NULL PRIMARY KEY,  -- 0 = N, 1 = NNE, ... 15 = NNW
    abbr_en  VARCHAR(3)  NOT NULL,
    abbr_da  VARCHAR(3)  NOT NULL,
    text_en  VARCHAR(30) NOT NULL,
    text_da  VARCHAR(30) NOT NULL
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

INSERT INTO winddirection (id, abbr_en, abbr_da, text_en, text_da) VALUES
( 0, 'N',   'N',   'North',           'Nord'),
( 1, 'NNE', 'NNØ', 'North-northeast', 'Nord-nordøst'),
( 2, 'NE',  'NØ',  'Northeast',       'Nordøst'),
( 3, 'ENE', 'ØNØ', 'East-northeast',  'Øst-nordøst'),
( 4, 'E',   'Ø',   'East',            'Øst'),
( 5, 'ESE', 'ØSØ', 'East-southeast',  'Øst-sydøst'),
( 6, 'SE',  'SØ',  'Southeast',       'Sydøst'),
( 7, 'SSE', 'SSØ', 'South-southeast', 'Syd-sydøst'),
( 8, 'S',   'S',   'South',           'Syd'),
( 9, 'SSW', 'SSV', 'South-southwest', 'Syd-sydvest'),
(10, 'SW',  'SV',  'Southwest',       'Sydvest'),
(11, 'WSW', 'VSV', 'West-southwest',  'Vest-sydvest'),
(12, 'W',   'V',   'West',            'Vest'),
(13, 'WNW', 'VNV', 'West-northwest',  'Vest-nordvest'),
(14, 'NW',  'NV',  'Northwest',       'Nordvest'),
(15, 'NNW', 'NNV', 'North-northwest', 'Nord-nordvest')
ON DUPLICATE KEY UPDATE abbr_en = VALUES(abbr_en), abbr_da = VALUES(abbr_da),
                        text_en = VALUES(text_en), text_da = VALUES(text_da);

-- ---------------------------------------------------------------------------
-- Precipitation (mm per hour)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS precipitation (
    id      TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    min_mm  DECIMAL(6,1) NOT NULL,  -- inclusive
    max_mm  DECIMAL(6,1) NOT NULL,  -- exclusive
    text_en VARCHAR(50) NOT NULL,
    text_da VARCHAR(50) NOT NULL
) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

INSERT INTO precipitation (id, min_mm, max_mm, text_en, text_da) VALUES
(1,  0.0,    0.1, 'None',                     'Ingen nedbør'),
(2,  0.1,    2.5, 'Light precipitation',      'Let nedbør'),
(3,  2.5,    7.6, 'Moderate precipitation',   'Moderat nedbør'),
(4,  7.6,   15.0, 'Heavy precipitation',      'Kraftig nedbør'),
(5, 15.0, 9999.0, 'Very heavy precipitation', 'Meget kraftig nedbør')
ON DUPLICATE KEY UPDATE text_en = VALUES(text_en), text_da = VALUES(text_da);

-- ---------------------------------------------------------------------------
-- View: weather data with English and Danish descriptions
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW weather_hourly_described AS
SELECT
    w.location,
    w.time,
    w.latitude,
    w.longitude,
    w.temperature_2m,
    w.relative_humidity_2m,
    w.pressure_msl,

    w.cloud_cover,
    cc.text_en AS cloud_cover_en,
    cc.text_da AS cloud_cover_da,

    w.precipitation,
    p.text_en  AS precipitation_en,
    p.text_da  AS precipitation_da,

    w.wind_speed_10m,
    ws.beaufort,
    ws.text_en AS wind_speed_en,
    ws.text_da AS wind_speed_da,

    w.wind_direction_10m,
    wd.abbr_en AS wind_direction_abbr_en,
    wd.abbr_da AS wind_direction_abbr_da,
    wd.text_en AS wind_direction_en,
    wd.text_da AS wind_direction_da
FROM weather_hourly w
LEFT JOIN cloudcover cc
       ON ROUND(w.cloud_cover) BETWEEN cc.min_pct AND cc.max_pct
LEFT JOIN precipitation p
       ON w.precipitation >= p.min_mm AND w.precipitation < p.max_mm
LEFT JOIN windspeed ws
       ON w.wind_speed_10m >= ws.min_kmh AND w.wind_speed_10m < ws.max_kmh
LEFT JOIN winddirection wd
       ON wd.id = FLOOR(MOD(w.wind_direction_10m + 11.25, 360) / 22.5);
