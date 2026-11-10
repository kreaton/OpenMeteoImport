# OpenMeteoImport

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
  "ConnectionStrings": {
    "Weather": ""
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
| `ConnectionStrings:Weather` | MariaDB/MySQL connection string, e.g. `Server=localhost;Port=3306;Database=weather;User=root;Password=secret`. Leave empty to skip the database import. |
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
2. `appsettings.{OPENMETEO_ENVIRONMENT}.json`, e.g. `appsettings.Local.json` when `OPENMETEO_ENVIRONMENT=Local`
   (`appsettings.Local.json` is git-ignored — a good place for a connection string with a password)
3. Environment variables prefixed `OPENMETEO_`, using `__` as separator, e.g.
   `OPENMETEO_ConnectionStrings__Weather`, `OPENMETEO_Location__Latitude`
4. Command-line arguments, e.g.

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

[`schema.sql`](schema.sql) creates the same table plus lookup tables (cloud cover, Beaufort wind speed, wind
direction, precipitation intensity — English and Danish texts) and a `weather_hourly_described` view joining them:

```bash
mysql -u root -p weather < schema.sql
```

Rows are upserted (`INSERT ... ON DUPLICATE KEY UPDATE`) in batches of 500 inside a single transaction, so re-running
the tool for an overlapping period simply updates existing rows.

## Notes

- Data is requested one calendar year at a time to keep responses small.
- Open-Meteo's free API is for non-commercial use; see their [terms](https://open-meteo.com/en/terms).
