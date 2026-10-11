using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.Span;
using SmartParking.Api.Endpoints;
using SmartParking.Api.Infrastructure;
using SmartParking.Application.Common;
using SmartParking.Application.Observability;
using SmartParking.Application.Validation;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure;
using SmartParking.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. Structured Logging (Serilog + Trace/Span Enrichment)
// ============================================================================
builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithSpan()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
});

// ============================================================================
// 2. Database, HybridCache, Redis, and Resilience Pipelines
// ============================================================================
builder.Services.AddSmartParkingInfrastructure(builder.Configuration);
builder.Services.AddSingleton<SmartParkingMetrics>();
builder.Services.AddScoped<CacheEvictionService>();
builder.Services.AddValidatorsFromAssemblyContaining<UserLoginValidator>();

// ============================================================================
// 3. Output Caching & Feature Management
// ============================================================================
builder.Services.AddOutputCache();
builder.Services.AddFeatureManagement(builder.Configuration.GetSection("FeatureManagement"));

// ============================================================================
// 4. Centralized Exception Handling (Chained IExceptionHandler + ProblemDetails)
// ============================================================================
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<ParkingConflictExceptionHandler>();
builder.Services.AddExceptionHandler<EntityNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<InvalidCredentialsExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ============================================================================
// 5. API Versioning (Supports both /api/SmartParking/* and /api/v1/SmartParking/*)
// ============================================================================
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ============================================================================
// 6. Rate Limiting (Auth, Write, and Read Partitioned Policies)
// ============================================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        await ExceptionResponseHelper.WriteErrorAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Too Many Requests",
            "Rate limit exceeded. Please wait before retrying.",
            ct: ct);
    };

    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon-auth",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy(RateLimitPolicies.Write, httpContext =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon-write",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                TokensPerPeriod = 30,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 2
            }));

    options.AddPolicy(RateLimitPolicies.Read, httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon-read",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 5
            }));
});

// ============================================================================
// 7. Observability (OpenTelemetry Tracing + Metrics + Prometheus + OTLP)
// ============================================================================
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "SmartParking.Api",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(SmartParkingMetrics.ActivitySourceName)
            .AddAspNetCoreInstrumentation(o => o.RecordException = true)
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation(o => o.RecordException = true);

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(SmartParkingMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();

        var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

// ============================================================================
// 8. Health Checks (/health/live and /health/ready)
// ============================================================================
var connString = builder.Configuration.GetConnectionString("SmartParkingDb")
    ?? "Server=(localdb)\\mssqllocaldb;Database=SmartParkingDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API process is responsive."), tags: ["live"])
    .AddSqlServer(
        connectionString: connString,
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "db"],
        timeout: TimeSpan.FromSeconds(5))
    .AddDbContextCheck<SmartParkingDbContext>(
        name: "efcore-dbcontext",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "db"]);

// ============================================================================
// 9. CORS & .NET 10 OpenAPI + Swagger UI + Scalar API Reference
// ============================================================================
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Smart Parking Web API (.NET 10)";
        document.Info.Version = "v1.0";
        document.Info.Description =
            "Production-grade Smart Parking REST API built with .NET 10 and SQL Server. " +
            "Includes Health Checks, OpenTelemetry, Rate Limiting, API Versioning, HybridCache, Feature Management, and Polly v8 Resilience.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// ============================================================================
// 10. HTTP Request Pipeline
// ============================================================================
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();

// Expose OpenAPI JSON at both /openapi/v1.json and /swagger/v1/swagger.json
app.MapOpenApi();
app.MapOpenApi("/swagger/{documentName}/swagger.json");

// Interactive Swagger UI (/swagger)
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Parking API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Smart Parking API (.NET 10) - Swagger UI";
    options.DisplayRequestDuration();
    options.EnableDeepLinking();
    options.EnableFilter();
    options.EnableTryItOutByDefault();
});

// Interactive Scalar API Reference (/scalar/v1)
app.MapScalarApiReference(options =>
{
    options.WithTitle("Smart Parking API (.NET 10)");
});

// Prometheus scraping endpoint (/metrics)
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// Health Check Endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse
});

// ============================================================================
// 11. Map All 33 SmartParking Endpoints (Unversioned + Versioned v1 Routes)
// ============================================================================
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Primary route matching https://freeprojectapi.com/apis/smart-parking (/api/SmartParking/*)
var primaryGroup = app.MapGroup("/api/SmartParking")
    .WithApiVersionSet(versionSet)
    .WithTags("SmartParking");

primaryGroup.MapUserEndpoints();
primaryGroup.MapClientEndpoints();
primaryGroup.MapSiteEndpoints();
primaryGroup.MapFloorEndpoints();
primaryGroup.MapParkingEndpoints();

// Versioned route (/api/v1/SmartParking/*)
var versionedGroup = app.MapGroup("/api/v{version:apiVersion}/SmartParking")
    .WithApiVersionSet(versionSet)
    .WithTags("SmartParking (Versioned)");

versionedGroup.MapUserEndpoints("V1_");
versionedGroup.MapClientEndpoints("V1_");
versionedGroup.MapSiteEndpoints("V1_");
versionedGroup.MapFloorEndpoints("V1_");
versionedGroup.MapParkingEndpoints("V1_");

// Optional Database Auto-Migration / Seeding on Startup in Development
if (app.Configuration.GetValue<bool>("Database:AutoMigrateAndSeed"))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        await db.Database.EnsureCreatedAsync();
        await DatabaseSeeder.SeedAsync(db, hasher, logger);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Skipping database auto-seeding because SQL Server is not reachable yet.");
    }
}

app.Run();
