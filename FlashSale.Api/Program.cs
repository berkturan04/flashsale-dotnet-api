using System.Net.Sockets;
using FlashSale.Api;
using FlashSale.Api.Common;
using FlashSale.Api.Features.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// .NET 10/11 Yerleşik OpenAPI Desteği
builder.Services.AddOpenApi();

// 1. Altyapı Bağlantıları (Uygulama boyunca tek nesne - Singleton)
var postgresConn = builder.Configuration.GetConnectionString("Postgres")!;
var redisConn = builder.Configuration.GetConnectionString("Redis")!;

builder.Services.AddNpgsqlDataSource(postgresConn);
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConn));
// GÜN 3 & 4: CQRS, Otomatik Validation Pipeline ve Global Hata Yönetimi
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConn));

// 2. GÜN 1 (%20 ODAK): Dependency Injection Yaşam Döngüsü Laboratuvarı
builder.Services.AddSingleton<ISingletonTracker, LifecycleTracker>();
builder.Services.AddScoped<IScopedTracker, LifecycleTracker>();
builder.Services.AddTransient<ITransientTracker, LifecycleTracker>();

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar/v1 adresinde modern API arayüzü
}

// GÜN 1 TESLİMAT UÇ NOKTASI: Altyapı ve DI Yaşam Döngüsü Doğrulaması
app.MapGet("/api/day1-check", async (
    NpgsqlDataSource dbDataSource,
    IConnectionMultiplexer redis,
    IConfiguration config,
    ISingletonTracker singleton1,
    ISingletonTracker singleton2,
    IScopedTracker scoped1,
    IScopedTracker scoped2,
    ITransientTracker transient1,
    ITransientTracker transient2) =>
{
    // PostgreSQL Ping
    await using var cmd = dbDataSource.CreateCommand("SELECT 1;");
    var pgResult = await cmd.ExecuteScalarAsync();

    // Redis Ping
    var redisDb = redis.GetDatabase();
    var redisPing = await redisDb.PingAsync();

    // RabbitMQ Port Kontrolü
    using var tcpClient = new TcpClient();
    await tcpClient.ConnectAsync(
        config.GetConnectionString("RabbitMqHost")!,
        int.Parse(config.GetConnectionString("RabbitMqPort")!));

    return Results.Ok(new
    {
        Infrastructure = new
        {
            PostgreSql = pgResult?.ToString() == "1" ? "Connected" : "Failed",
            RedisLatencyMs = redisPing.TotalMilliseconds,
            RabbitMqAmqpPort = tcpClient.Connected ? "Connected" : "Failed"
        },
        DependencyInjectionLab = new
        {
            // Uygulama kapanana kadar asla değişmez
            Singleton_Call1 = singleton1.InstanceId,
            Singleton_Call2 = singleton2.InstanceId,

            // Aynı HTTP isteği içinde aynıdır, sayfayı yenileyince değişir
            Scoped_Call1 = scoped1.InstanceId,
            Scoped_Call2 = scoped2.InstanceId,

            // Aynı HTTP isteği içinde bile her çağrıldığında yeni nesne üretir
            Transient_Call1 = transient1.InstanceId,
            Transient_Call2 = transient2.InstanceId
        }
    });
});

// GÜN 2 TESLİMAT UÇ NOKTASI: PostgreSQL Tablo ve İndeks Doğrulaması
app.MapGet("/api/day2-check", async (AppDbContext db, NpgsqlDataSource dataSource) =>
{
    var indexes = new List<object>();

    await using var cmd = dataSource.CreateCommand(@"
        SELECT tablename, indexname, indexdef 
        FROM pg_indexes 
        WHERE schemaname = 'public' AND indexname LIKE 'IX_%';");

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        indexes.Add(new
        {
            Table = reader.GetString(0),
            IndexName = reader.GetString(1),
            Definition = reader.GetString(2)
        });
    }

    return Results.Ok(new
    {
        SchemaStatus = "EF Core Code-First Ready",
        ActiveCustomIndexes = indexes
    });
});
// GÜN 3 TESLİMAT UÇ NOKTALARI: Vertical Slice Events
CreateEvent.MapEndpoint(app);
GetEvents.MapEndpoint(app);
// GÜN 4 TESLİMAT TESTİ: Kontrolsüz Çökme Simülasyonu
app.MapGet("/api/day4-crash-test", () =>
{
    throw new InvalidOperationException("Veritabanı bağlantısı aniden koptu (Hassas iç bilgi sızmamalı)!");
});
app.Run();

// DI Yaşam Döngüsü Test Sınıfları
public interface ITracker { Guid InstanceId { get; } }
public interface ISingletonTracker : ITracker { }
public interface IScopedTracker : ITracker { }
public interface ITransientTracker : ITracker { }

public class LifecycleTracker : ISingletonTracker, IScopedTracker, ITransientTracker
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}