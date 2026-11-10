using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

// ---- Config ---------------------------------------------------------------
// Sources, later ones win: appsettings.json, appsettings.{OPENMETEO_ENVIRONMENT}.json,
// environment variables prefixed OPENMETEO_ (e.g. OPENMETEO_Location__Latitude),
// then command-line args (e.g. --Location:Name=Aarhus --Extract:StartDate=2023-01-01).
var environment = Environment.GetEnvironmentVariable("OPENMETEO_ENVIRONMENT");
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true)
    .AddEnvironmentVariables("OPENMETEO_")
    .AddCommandLine(args)
    .Build();

var settings = config.Get<ImportSettings>() ?? new ImportSettings();
var location = settings.Location;
var connStr = config.GetConnectionString("Weather");

if (string.IsNullOrWhiteSpace(location.Name))
    return Fail("Location:Name must be set.");
if (location.Latitude is < -90 or > 90 || location.Longitude is < -180 or > 180)
    return Fail("Location:Latitude/Longitude are out of range.");
if (!Regex.IsMatch(settings.Output.TableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
    return Fail($"Output:TableName '{settings.Output.TableName}' is not a valid table name.");

var startDate = settings.Extract.StartDate;
// The archive API lags a few days behind real time, so default to five days ago.
var endDate = settings.Extract.EndDate ?? DateOnly.FromDateTime(DateTime.Today).AddDays(-5);
if (endDate < startDate)
    return Fail($"Extract:EndDate ({endDate:yyyy-MM-dd}) is before Extract:StartDate ({startDate:yyyy-MM-dd}).");

string[] variables =
[
    "temperature_2m", "relative_humidity_2m", "precipitation",
    "pressure_msl", "wind_speed_10m", "wind_direction_10m", "cloud_cover"
];

Console.WriteLine(FormattableString.Invariant(
    $"Location: {location.Name} ({location.Latitude}, {location.Longitude}), timezone {location.Timezone}"));
Console.WriteLine($"Period:   {startDate:yyyy-MM-dd} -> {endDate:yyyy-MM-dd}");

// ---- Fetch (one request per calendar year) -------------------------------
using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
var rows = new List<WeatherRow>();

for (var from = startDate; from <= endDate;)
{
    var to = new DateOnly(from.Year, 12, 31);
    if (to > endDate) to = endDate;

    var url = FormattableString.Invariant(
        $"https://archive-api.open-meteo.com/v1/archive?latitude={location.Latitude}&longitude={location.Longitude}&start_date={from:yyyy-MM-dd}&end_date={to:yyyy-MM-dd}&hourly={string.Join(',', variables)}&timezone={Uri.EscapeDataString(location.Timezone)}");

    Console.WriteLine($"Fetching {from:yyyy-MM-dd} -> {to:yyyy-MM-dd} ...");
    using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
    var hourly = doc.RootElement.GetProperty("hourly");
    var times = hourly.GetProperty("time");

    for (var i = 0; i < times.GetArrayLength(); i++)
    {
        double? V(string name)
        {
            var el = hourly.GetProperty(name)[i];
            return el.ValueKind == JsonValueKind.Null ? null : el.GetDouble();
        }

        rows.Add(new WeatherRow(
            DateTime.ParseExact(times[i].GetString()!, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture),
            V("temperature_2m"), V("relative_humidity_2m"), V("precipitation"),
            V("pressure_msl"), V("wind_speed_10m"), V("wind_direction_10m"), V("cloud_cover")));
    }

    from = to.AddDays(1);
}

Console.WriteLine($"Fetched {rows.Count} hourly rows.");

// ---- CSV -----------------------------------------------------------------
if (!string.IsNullOrWhiteSpace(settings.Output.CsvPath))
{
    var sb = new StringBuilder();
    sb.AppendLine("location,time,temperature_2m,relative_humidity_2m,precipitation,pressure_msl,wind_speed_10m,wind_direction_10m,cloud_cover");
    var locationField = CsvField(location.Name);
    foreach (var r in rows)
    {
        sb.AppendLine(string.Join(',',
            locationField,
            r.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            F(r.Temperature), F(r.Humidity), F(r.Precipitation),
            F(r.Pressure), F(r.WindSpeed), F(r.WindDirection), F(r.CloudCover)));
    }
    await File.WriteAllTextAsync(settings.Output.CsvPath, sb.ToString());
    Console.WriteLine($"Wrote {Path.GetFullPath(settings.Output.CsvPath)}");
}

// ---- MariaDB / MySQL -----------------------------------------------------
if (string.IsNullOrWhiteSpace(connStr))
{
    Console.WriteLine("ConnectionStrings:Weather not set - skipping database import.");
    return 0;
}

var table = settings.Output.TableName;
await using var conn = new MySqlConnection(connStr);
await conn.OpenAsync();

await using (var create = new MySqlCommand($"""
    CREATE TABLE IF NOT EXISTS `{table}` (
        location VARCHAR(100) NOT NULL,
        time DATETIME NOT NULL,
        latitude DOUBLE NOT NULL,
        longitude DOUBLE NOT NULL,
        temperature_2m DOUBLE NULL,
        relative_humidity_2m DOUBLE NULL,
        precipitation DOUBLE NULL,
        pressure_msl DOUBLE NULL,
        wind_speed_10m DOUBLE NULL,
        wind_direction_10m DOUBLE NULL,
        cloud_cover DOUBLE NULL,
        PRIMARY KEY (location, time)
    )
    """, conn))
{
    await create.ExecuteNonQueryAsync();
}

// Upsert in batches inside one transaction, so re-running just adds/updates rows.
await using var tx = await conn.BeginTransactionAsync();
const int batchSize = 500;

for (var i = 0; i < rows.Count; i += batchSize)
{
    var batch = rows.Skip(i).Take(batchSize).ToList();
    var sql = new StringBuilder($"""
        INSERT INTO `{table}`
            (location, time, latitude, longitude, temperature_2m, relative_humidity_2m, precipitation, pressure_msl, wind_speed_10m, wind_direction_10m, cloud_cover)
        VALUES
        """);

    await using var cmd = new MySqlCommand { Connection = conn, Transaction = tx };
    cmd.Parameters.AddWithValue("@loc", location.Name);
    cmd.Parameters.AddWithValue("@lat", location.Latitude);
    cmd.Parameters.AddWithValue("@lon", location.Longitude);
    for (var j = 0; j < batch.Count; j++)
    {
        sql.Append(j == 0 ? "\n" : ",\n");
        sql.Append($"(@loc,@t{j},@lat,@lon,@a{j},@b{j},@c{j},@d{j},@e{j},@f{j},@g{j})");
        var r = batch[j];
        cmd.Parameters.AddWithValue($"@t{j}", r.Time);
        cmd.Parameters.AddWithValue($"@a{j}", (object?)r.Temperature ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@b{j}", (object?)r.Humidity ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@c{j}", (object?)r.Precipitation ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@d{j}", (object?)r.Pressure ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@e{j}", (object?)r.WindSpeed ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@f{j}", (object?)r.WindDirection ?? DBNull.Value);
        cmd.Parameters.AddWithValue($"@g{j}", (object?)r.CloudCover ?? DBNull.Value);
    }
    sql.Append("""

        ON DUPLICATE KEY UPDATE
            latitude = VALUES(latitude),
            longitude = VALUES(longitude),
            temperature_2m = VALUES(temperature_2m),
            relative_humidity_2m = VALUES(relative_humidity_2m),
            precipitation = VALUES(precipitation),
            pressure_msl = VALUES(pressure_msl),
            wind_speed_10m = VALUES(wind_speed_10m),
            wind_direction_10m = VALUES(wind_direction_10m),
            cloud_cover = VALUES(cloud_cover)
        """);
    cmd.CommandText = sql.ToString();
    await cmd.ExecuteNonQueryAsync();
}

await tx.CommitAsync();
Console.WriteLine($"Upserted {rows.Count} rows for '{location.Name}' into {table}.");
return 0;

// ---- Helpers -------------------------------------------------------------
static string F(double? v) => v?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";

static string CsvField(string s) =>
    s.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

static int Fail(string message)
{
    Console.Error.WriteLine($"Configuration error: {message}");
    return 1;
}

record WeatherRow(
    DateTime Time,
    double? Temperature,
    double? Humidity,
    double? Precipitation,
    double? Pressure,
    double? WindSpeed,
    double? WindDirection,
    double? CloudCover);

sealed class ImportSettings
{
    public LocationSettings Location { get; set; } = new();
    public ExtractSettings Extract { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}

sealed class LocationSettings
{
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = "auto";
}

sealed class ExtractSettings
{
    public DateOnly StartDate { get; set; } = new(2024, 1, 1);
    public DateOnly? EndDate { get; set; }
}

sealed class OutputSettings
{
    public string? CsvPath { get; set; } = "weather.csv";
    public string TableName { get; set; } = "weather_hourly";
}
