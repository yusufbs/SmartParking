using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Persistence;

namespace SmartParking.Infrastructure;

public static class DependencyInjection
{
    public const string ParkingCheckInResiliencePipeline = "parking-checkin-pipeline";

    public static IServiceCollection AddSmartParkingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartParkingDb")
            ?? "Server=(localdb)\\mssqllocaldb;Database=SmartParkingDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        // 1. SQL Server + EF Core 10 with built-in execution strategy retry resilience
        services.AddDbContextPool<SmartParkingDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(15);
                sqlOptions.MaxBatchSize(100);
            });
        });

        // 2. Password hashing
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        // 3. Multi-tier Caching: HybridCache (L1 In-Memory + optional L2 Redis)
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "SmartParking:";
            });
        }

        #pragma warning disable EXTEXP0018
        services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 1024 * 1024; // 1 MB
            options.MaximumKeyLength = 512;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(5),
                LocalCacheExpiration = TimeSpan.FromMinutes(2)
            };
        });
        #pragma warning restore EXTEXP0018

        // 4. Resilience Pipeline (Polly v8) for Parking Check-In & Concurrency Handling
        services.AddResiliencePipeline(ParkingCheckInResiliencePipeline, builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder()
                        .Handle<DbUpdateConcurrencyException>()
                        .Handle<SqlException>(ex => ex.Number is 1205 or -2), // Deadlock or timeout
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(50),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<SqlException>(),
                    FailureRatio = 0.5,
                    MinimumThroughput = 20,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15)
                })
                .AddTimeout(TimeSpan.FromSeconds(5));
        });

        return services;
    }
}
