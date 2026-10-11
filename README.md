# Smart Parking Web API (.NET 10 & SQL Server)

A production-grade, high-performance **Smart Parking Web API** built with **.NET 10 (`net10.0`, C# 14)** and **Microsoft SQL Server** (EF Core 10). Fully contract-compatible with the [FreeProjectAPI Smart Parking specification](https://freeprojectapi.com/apis/smart-parking) (all **33 REST endpoints** and **12 DTO schemas**).

---

## Architecture & Project Structure

```text
C:\Projects\SmartParking
├── SmartParking.slnx
├── .gitignore
├── README.md
└── src
    ├── SmartParking.Domain           # Entities, RowVersion concurrency tokens, Domain Exceptions
    ├── SmartParking.Application      # DTO Contracts, FluentValidation, FeatureFlags, Metrics, LoggerMessage
    ├── SmartParking.Infrastructure   # EF Core 10 DbContext, Compiled Queries, SQL DDL, HybridCache, Polly v8
    └── SmartParking.Api              # Minimal API Endpoint Groups, Middleware, ExceptionHandlers, OpenTelemetry
```

---

## Enterprise Cross-Cutting Features

| Capability | Implementation Details |
| :--- | :--- |
| **High-Performance Data Access** | `AddDbContextPool<SmartParkingDbContext>`, `AsNoTracking()` read projections, and **EF Core 10 Compiled Queries** (`EF.CompileAsyncQuery`) for hot paths (`GetAllClients`, `GetAllFloor`, `GetAllActiveParking`). |
| **Health Checks & Metrics** | `/health/live` (liveness) and `/health/ready` (SQL Server + EF Core `DbContext` readiness) with detailed JSON output. Custom `IMeterFactory` instruments in `SmartParkingMetrics` (`smartparking.parkings.checkin.count`, `smartparking.parkings.checkout.count`, `smartparking.parkings.active`, `smartparking.parkings.duration.minutes`, `smartparking.revenue.collected`). |
| **Observability (OpenTelemetry)** | Distributed tracing (`ASP.NET Core`, `HttpClient`, `SqlClient`, and custom `ActivitySource("SmartParking.Domain")` spans) + runtime/application metrics exported via Prometheus (`/metrics`) and OTLP, plus `X-Correlation-Id` propagation middleware. |
| **Rate Limiting** | Partitioned policies by client IP: `auth-policy` (Fixed Window: 10 req/min on `/login` & `/register`), `write-policy` (Token Bucket: 30 req/min on mutations), and `read-policy` (Sliding Window: 120 req/min on queries). Returns HTTP `429 Too Many Requests` with `Retry-After`. |
| **API Versioning** | Supports both default unversioned routes (`/api/SmartParking/*`) and explicit versioned routes (`/api/v1/SmartParking/*`), plus `X-Api-Version` header and `?api-version=1.0` query string readers. |
| **Structured Logging** | **Serilog** enriched with `CorrelationId`, `TraceId`, `SpanId`, machine name, and environment, paired with compile-time `[LoggerMessage]` source-generated methods and password redaction. |
| **Multi-Tier Caching** | .NET 10 `HybridCache` (L1 In-Memory + optional L2 Redis) and `OutputCache` with automatic tag-based eviction (`clients`, `sites`, `buildings`, `floors`, `parkings-active`) on write operations. |
| **Feature Management** | `Microsoft.FeatureManagement` flags: `EnableDynamicSurgePricing` (1.25x surge when floor occupancy $\ge$ 80%), `EnableStrictSpotValidation`, and `EnableDetailedAdminAnalytics`. |
| **Exception Handling** | Chained `IExceptionHandler` pipeline (`ValidationExceptionHandler`, `ParkingConflictExceptionHandler`, `EntityNotFoundExceptionHandler`, `InvalidCredentialsExceptionHandler`, `GlobalExceptionHandler`) returning RFC 9457 `ProblemDetails` or `ApiResponse` envelopes (`X-Response-Envelope: ApiResponse` or `?envelope=apiResponse`). |
| **Resilience** | EF Core SQL Server `EnableRetryOnFailure` execution strategy + Polly v8 `ResiliencePipeline` (`"parking-checkin-pipeline"`) with exponential backoff + jitter retry on concurrency/deadlock errors, circuit breaker, and 5s timeout. |

---

## Getting Started

### Prerequisites
- **.NET 10 SDK** (`10.0.100` or newer)
- **Microsoft SQL Server** (LocalDB, SQL Server Developer/Express, or Azure SQL)
- **Redis** *(Optional — if `ConnectionStrings:Redis` is empty, `HybridCache` automatically operates in high-speed L1 in-memory mode)*

### Configuration (`src/SmartParking.Api/appsettings.json`)
Update the `SmartParkingDb` connection string if not using SQL Server LocalDB:
```json
{
  "ConnectionStrings": {
    "SmartParkingDb": "Server=(localdb)\\mssqllocaldb;Database=SmartParkingDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
    "Redis": ""
  },
  "Database": {
    "AutoMigrateAndSeed": true
  },
  "FeatureManagement": {
    "EnableDynamicSurgePricing": true,
    "EnableStrictSpotValidation": true,
    "EnableDetailedAdminAnalytics": true
  }
}
```

### Database Setup
When `Database:AutoMigrateAndSeed` is `true`, the application automatically creates the schema and seeds initial sample data on startup.
To provision the database manually in SQL Server Management Studio (SSMS) or `sqlcmd`, run:
- `src/SmartParking.Infrastructure/Persistence/Scripts/001_InitialSchema.sql`

Key SQL Server index highlights:
- `UX_Parkings_ActiveSpot`: Filtered unique index `ON dbo.Parkings (FloorId, ParkSpotNo) WHERE OutTime IS NULL AND ParkSpotNo > 0` preventing concurrent double-booking of the same spot.
- `IX_Parkings_Search`: Covering index on `(FloorId, ParkDate DESC, VehicleNo)` accelerating `GetAdminAllParkings`.

### Build & Run
```powershell
dotnet build "C:\Projects\SmartParking\SmartParking.slnx" -c Release
dotnet run --project "C:\Projects\SmartParking\src\SmartParking.Api\SmartParking.Api.csproj"
```

---

## Operational & Documentation Endpoints

| Endpoint | Description |
| :--- | :--- |
| `GET /swagger` | Interactive **Swagger UI** (with Try-It-Out, filter search, and request duration) |
| `GET /swagger/v1/swagger.json` | Swagger / OpenAPI v1 JSON Specification |
| `GET /scalar/v1` | Interactive **Scalar** API Documentation & Tester |
| `GET /openapi/v1.json` | .NET 10 OpenAPI v1 Specification |
| `GET /health/live` | Liveness probe (process responsiveness) |
| `GET /health/ready` | Readiness probe (SQL Server & EF Core `DbContext` connectivity) |
| `GET /metrics` | OpenTelemetry **Prometheus** metrics scraping endpoint |

---

## Complete API Reference (33 Endpoints)

All endpoints are available under both `/api/SmartParking/...` and `/api/v1/SmartParking/...`.

### 1. Users & Authentication (6 Endpoints)
| Method | Route | Query / Body | Response |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/SmartParking/getAllUsersByClientId` | `?clientId={int}` | `UserMasterView[]` |
| `GET` | `/api/SmartParking/getUserById` | `?id={int}` | `UserMasterNew` |
| `POST` | `/api/SmartParking/register` | Body: `UserMasterViewClient` | `UserMasterNew` |
| `POST` | `/api/SmartParking/login` | Body: `UserLogin` | `UserMasterNew` |
| `PUT` | `/api/SmartParking/updateUser` | `?id={int}`, Body: `UserMasterNew` | `ApiResponse` |
| `DELETE` | `/api/SmartParking/deleteUserById` | `?id={int}` | `ApiResponse` |

### 2. Clients (4 Endpoints)
| Method | Route | Query / Body | Response |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/SmartParking/GetAllClients` | — | `ApiResponse<GetWClients[]>` |
| `POST` | `/api/SmartParking/AddClient` | Body: `GetWClients` | `ApiResponse<GetWClients>` |
| `POST` | `/api/SmartParking/UpdateClient` | Body: `GetWClients` | `ApiResponse<GetWClients>` |
| `POST` | `/api/SmartParking/DeleteClient` | `?id={int}` | `ApiResponse` |

### 3. Sites & Buildings (7 Endpoints)
| Method | Route | Query / Body | Response |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/SmartParking/GetAllSites` | `?clientid={int?}` | `ApiResponse<SiteListItemDto[]>` |
| `GET` | `/api/SmartParking/GetSitesByClientId` | `?id={int}` | `ApiResponse<SiteListItemDto[]>` |
| `GET` | `/api/SmartParking/GetSiteById` | `?id={int}` | `ApiResponse<GetVSiteMasterView>` |
| `POST` | `/api/SmartParking/AddClientSite` | Body: `GetVSiteMasterView` (with `getVBuildingss`) | `ApiResponse<GetVSiteMasterView>` |
| `POST` | `/api/SmartParking/UpdateSite` | Body: `GetVSiteMasterView` (with `getVBuildingss`) | `ApiResponse<GetVSiteMasterView>` |
| `POST` | `/api/SmartParking/DeleteSite` | `?id={int}` | `ApiResponse` |
| `GET` | `/api/SmartParking/GetBuildingBySiteId` | `?id={int}` | `ApiResponse<GetVBuildings[]>` |

### 4. Floors (6 Endpoints)
| Method | Route | Query / Body | Response |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/SmartParking/GetAllFloor` | — | `ApiResponse<FloorListItemDto[]>` |
| `GET` | `/api/SmartParking/GetFloorsByBuildingId` | `?id={int}` | `ApiResponse<FloorListItemDto[]>` |
| `GET` | `/api/SmartParking/GetFloorsBySiteId` | `?id={int}` | `ApiResponse<FloorListItemDto[]>` |
| `POST` | `/api/SmartParking/AddFloor` | Body: `GetVFloors` | `ApiResponse<GetVFloors>` |
| `POST` | `/api/SmartParking/UpdateFloor` | Body: `GetVFloors` | `ApiResponse<GetVFloors>` |
| `POST` | `/api/SmartParking/DeleteFloor` | `?id={int}` | `ApiResponse` |

### 5. Parking Operations & Admin Search (10 Endpoints)
| Method | Route | Query / Body | Response |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/SmartParking/GetAllParking` | — | `ApiResponse<ParkingListItemDto[]>` |
| `GET` | `/api/SmartParking/GetAllActiveParking` | — | `ApiResponse<ParkingListItemDto[]>` |
| `GET` | `/api/SmartParking/GetAllParkingByClientId` | `?id={int}` | `ApiResponse<ParkingListItemDto[]>` |
| `GET` | `/api/SmartParking/GetAllParkingBySite` | `?id={int}` | `ApiResponse<ParkingListItemDto[]>` |
| `GET` | `/api/SmartParking/GetAllParkingByBuilding` | `?id={int}` | `ApiResponse<ParkingListItemDto[]>` |
| `GET` | `/api/SmartParking/GetAllParkingByFloor` | `?id={int}` | `ApiResponse<ParkingListItemDto[]>` |
| `POST` | `/api/SmartParking/AddParking` | Body: `GetVParkings` | `ApiResponse<GetVParkings>` |
| `POST` | `/api/SmartParking/MarExit` *(+ `/MarkExit`)* | Body: `ExitParking` | `ApiResponse` |
| `POST` | `/api/SmartParking/DeleteParking` | `?id={int}` | `ApiResponse` |
| `POST` | `/api/SmartParking/GetAdminAllParkings` | Body: `AllParkingSearch` | `ApiResponse<ParkingListItemDto[]>` |
