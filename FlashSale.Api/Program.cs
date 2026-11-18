using System.Net.Sockets;
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
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => 
    ConnectionMultiplexer.Connect(redisConn));

// 2. GÜN 1 (%20 ODAK): Dependency Injection Yaşam Döngüsü Laboratuvarı
builder.Services.AddSingleton<ISingletonTracker, LifecycleTracker>();
builder.Services.AddScoped<IScopedTracker, LifecycleTracker>();
builder.Services.AddTransient<ITransientTracker, LifecycleTracker>();

var app = builder.Build();

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