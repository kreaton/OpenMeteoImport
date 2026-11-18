# OpenMeteoImport

[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/kreaton/OpenMeteoImport?style=flat&logo=github&color=yellow)](https://github.com/kreaton/OpenMeteoImport/stargazers)

⭐ If OpenMeteoImport earns a place on your machine, [star it on GitHub](https://github.com/kreaton/OpenMeteoImport) — it's free, made by one developer, and stars are how other people find it.

A .NET 10 command-line tool that downloads **hourly historical weather** for any location from the free
[Open-Meteo Archive API](https://open-meteo.com/en/docs/historical-weather-api), writes it to a CSV file and
(optionally) upserts it into a **MariaDB / MySQL** table.

Fetched variables: `temperature_2m`, `relative_humidity_2m`, `precipitation`, `pressure_msl`,
`wind_speed_10m`, `wind_direction_10m`, `cloud_cover`.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Optional: a MariaDB or MySQL server (the table is created automatically)

## Build & run

```bash
dotnet build
dotnet run --project OpenMeteoImport
```

## Configuration

All settings live in [`OpenMeteoImport/appsettings.json`](OpenMeteoImport/appsettings.json):

```json
{
  "MySqlConnection": {
    "Server": "127.0.0.1",
    "Port": "3306",
    "Database": "weather",
    "User ID": "user",
    "Password": "set-in-appsettings.debug.json",
    "AllowLoadLocalInfile": true
  },
  "Location": {
    "Name": "Copenhagen",
    "Latitude": 55.6761,
    "Longitude": 12.5683,
    "Timezone": "Europe/Copenhagen"
  },
  "Extract": {
    "StartDate": "2024-01-01",
    "EndDate": null
  },
  "Output": {
    "CsvPath": "weather.csv",
    "TableName": "weather_hourly"
  }
}
```

| Setting | Description |
| --- | --- |
| `MySqlConnection` | MariaDB/MySQL connection. Each key is a [MySqlConnector connection-string option](https://mysqlconnector.net/connection-options/) (`Server`, `Port`, `Database`, `User ID`, `Password`, ...), so more can be added. Leave `Server` empty to skip the database import. Keep the real password out of this file — see `appsettings.debug.json` below. |
| `Location:Name` | Label stored with every row (part of the table's primary key, so several locations can share one table). |
| `Location:Latitude` / `Location:Longitude` | Coordinates in decimal degrees. |
| `Location:Timezone` | IANA time zone for the returned timestamps, e.g. `Europe/Copenhagen`, or `auto`. |
| `Extract:StartDate` | First day to extract (`yyyy-MM-dd`). |
| `Extract:EndDate` | Last day to extract (`yyyy-MM-dd`). `null` = today minus 5 days (the archive lags a few days behind real time). |
| `Output:CsvPath` | CSV output file, relative to the current directory. Empty to skip the CSV. |
| `Output:TableName` | Database table to create/upsert into (letters, digits and `_` only). |

### Overriding settings

Configuration is layered; later sources win:

1. `appsettings.json` (next to the executable)
2. `appsettings.debug.json` — **Debug builds only**. It is git-ignored and not copied to Release/publish output,
   so it is the place for the database password. Create it next to `appsettings.json`; any other
   `MySqlConnection` key can be overridden here too:

   ```json
   {
     "MySqlConnection": {
       "Password": "secret"
     }
   }
   ```

3. Command-line arguments, e.g.

```bash
dotnet run --project OpenMeteoImport -- --Location:Name=Aarhus --Location:Latitude=56.1629 --Location:Longitude=10.2039 --Extract:StartDate=2023-01-01 --Extract:EndDate=2023-12-31
```

## Database

The tool creates the table if it does not exist:

| Column | Type |
| --- | --- |
| `location` | `VARCHAR(100)`, primary key part |
| `time` | `DATETIME` (local time in the configured time zone), primary key part |
| `latitude`, `longitude` | `DOUBLE` |
| `temperature_2m` (°C), `relative_humidity_2m` (%), `precipitation` (mm), `pressure_msl` (hPa), `wind_speed_10m` (km/h), `wind_direction_10m` (°), `cloud_cover` (%) | `DOUBLE NULL` |

[`schema_v2.sql`](schema_v2.sql) creates the same table plus lookup tables (cloud cover, Beaufort wind speed, wind
direction, precipitation intensity — English and Danish texts) and a `weather_hourly_described` view joining them:

```bash
mysql -u root -p weather < schema_v2.sql
```

Rows are upserted (`INSERT ... ON DUPLICATE KEY UPDATE`) in batches of 500 inside a single transaction, so re-running
the tool for an overlapping period simply updates existing rows.

## Notes

- Data is requested one calendar year at a time to keep responses small.
- Open-Meteo's free API is for non-commercial use; see their [terms](https://open-meteo.com/en/terms).

## License & Authorship

This project is distributed under the terms of the [MIT License](LICENSE).

**AI authorship note:** Most of the source code in this repository was generated with AI coding agents (Claude Code), under my direction and review. Purely AI-generated material may not be eligible for copyright protection in some jurisdictions. To the extent any part of this project is protected by copyright, it is licensed under the MIT License, and the license's warranty and liability disclaimers apply to the project as a whole.

<!-- notices:start -->
### Independent development

OpenMeteoImport is an independent, personal project developed by Per Hejndorf. It is not affiliated with, endorsed by or sponsored by Microsoft or any other rights holder (including Open-Meteo). Product names and logos are trademarks of their respective owners and are used only to identify the services and tools the project works with.

### Third-party notices and asset provenance

[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) lists every third-party library, font and online service the project uses, with version, licence, source and whether it was modified. It also records where the icons, images, audio and video came from and how they are licensed.
<!-- notices:end -->
