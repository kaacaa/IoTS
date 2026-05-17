using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Konekcija na PostgreSQL
var connStr = $"Host={Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost"};" +
              $"Port=5432;Database={Environment.GetEnvironmentVariable("DB_NAME") ?? "iotdb"};" +
              $"Username={Environment.GetEnvironmentVariable("DB_USER") ?? "iotuser"};" +
              $"Password={Environment.GetEnvironmentVariable("DB_PASS") ?? "iotpass"}";

builder.Services.AddSingleton(NpgsqlDataSource.Create(connStr));
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseCors();
app.MapOpenApi();

// Swagger UI preko Scalar
app.MapScalarApiReference(static o => {
    o.Title = "IoT REST API";
    o.EndpointPathPrefix = "/api-docs/{documentName}";
});

// ── Scenario A: Upis ocitavanja ──────────────────────────────────────────────
app.MapPost("/readings", async (SensorReading reading, NpgsqlDataSource db) =>
{
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        INSERT INTO sensor_readings (ts, device_id, co, humidity, light, lpg, motion, smoke, temp)
        VALUES (NOW(), @device_id, @co, @humidity, @light, @lpg, @motion, @smoke, @temp)
        RETURNING id, ts
        """;
    cmd.Parameters.AddWithValue("device_id", reading.device_id);
    cmd.Parameters.AddWithValue("co",        reading.co);
    cmd.Parameters.AddWithValue("humidity",  reading.humidity);
    cmd.Parameters.AddWithValue("light",     reading.light);
    cmd.Parameters.AddWithValue("lpg",       reading.lpg);
    cmd.Parameters.AddWithValue("motion",    reading.motion);
    cmd.Parameters.AddWithValue("smoke",     reading.smoke);
    cmd.Parameters.AddWithValue("temp",      reading.temp);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    return Results.Created("/readings", new { id = reader.GetInt64(0), ts = reader.GetDateTime(1) });
})
.WithName("IngestReading")
.WithSummary("Scenario A — Upis novog ocitavanja");

// ── Scenario B: Selektivno citanje ───────────────────────────────────────────
app.MapGet("/readings/selective", async (NpgsqlDataSource db, string? device_id) =>
{
    var deviceId = device_id ?? "b8:27:eb:bf:9d:51";
    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT ts, temp, humidity FROM sensor_readings
        WHERE device_id = @device_id
        ORDER BY ts DESC LIMIT 100
        """;
    cmd.Parameters.AddWithValue("device_id", deviceId);

    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new { ts = reader.GetDateTime(0), temp = reader.GetDouble(1), humidity = reader.GetDouble(2) });

    return Results.Ok(results);
})
.WithName("SelectiveReadings")
.WithSummary("Scenario B — Selektivna polja temp i humidity");

// ── Scenario C: Agregacije ────────────────────────────────────────────────────
app.MapGet("/readings/aggregate", async (NpgsqlDataSource db, string? device_id, string? from, string? to) =>
{
    var deviceId = device_id ?? "b8:27:eb:bf:9d:51";
    var fromTs   = from ?? "2020-07-12";
    var toTs     = to   ?? "2026-05-17";

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = """
        SELECT
            device_id,
            DATE_TRUNC('hour', ts)  AS hour,
            AVG(temp)               AS avg_temp,
            AVG(humidity)           AS avg_humidity,
            AVG(co)                 AS avg_co,
            AVG(smoke)              AS avg_smoke,
            COUNT(*)                AS num_readings
        FROM sensor_readings
        WHERE device_id = @device_id
          AND ts >= @from::timestamptz
          AND ts <= @to::timestamptz
        GROUP BY device_id, DATE_TRUNC('hour', ts)
        ORDER BY hour DESC
        """;
    cmd.Parameters.AddWithValue("device_id", deviceId);
    cmd.Parameters.AddWithValue("from",      fromTs);
    cmd.Parameters.AddWithValue("to",        toTs);

    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new {
            device_id    = reader.GetString(0),
            hour         = reader.GetDateTime(1),
            avg_temp     = reader.GetDouble(2),
            avg_humidity = reader.GetDouble(3),
            avg_co       = reader.GetDouble(4),
            avg_smoke    = reader.GetDouble(5),
            num_readings = reader.GetInt64(6)
        });

    return Results.Ok(results);
})
.WithName("AggregateReadings")
.WithSummary("Scenario C — Agregacije po satu");

// ── Generalni listing ─────────────────────────────────────────────────────────
app.MapGet("/readings", async (NpgsqlDataSource db, int? limit, int? offset) =>
{
    var lim = Math.Min(limit ?? 50, 500);
    var off = offset ?? 0;

    await using var conn = await db.OpenConnectionAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, ts, device_id, co, humidity, light, lpg, motion, smoke, temp FROM sensor_readings ORDER BY ts DESC LIMIT @limit OFFSET @offset";
    cmd.Parameters.AddWithValue("limit",  lim);
    cmd.Parameters.AddWithValue("offset", off);

    var results = new List<object>();
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        results.Add(new {
            id        = reader.GetInt64(0),
            ts        = reader.GetDateTime(1),
            device_id = reader.GetString(2),
            co        = reader.GetDouble(3),
            humidity  = reader.GetDouble(4),
            light     = reader.GetBoolean(5),
            lpg       = reader.GetDouble(6),
            motion    = reader.GetBoolean(7),
            smoke     = reader.GetDouble(8),
            temp      = reader.GetDouble(9)
        });

    return Results.Ok(results);
})
.WithName("ListReadings")
.WithSummary("Lista ocitavanja sa paginacijom");

app.Run();

// ── Model ─────────────────────────────────────────────────────────────────────
record SensorReading(
    string device_id,
    double co,
    double humidity,
    bool   light,
    double lpg,
    bool   motion,
    double smoke,
    double temp
);