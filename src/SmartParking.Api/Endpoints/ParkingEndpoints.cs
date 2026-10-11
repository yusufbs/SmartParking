using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.FeatureManagement;
using Polly.Registry;
using SmartParking.Api.Infrastructure;
using SmartParking.Application.Common;
using SmartParking.Application.Contracts;
using SmartParking.Application.Observability;
using SmartParking.Domain.Entities;
using SmartParking.Domain.Exceptions;
using SmartParking.Infrastructure;
using SmartParking.Infrastructure.Persistence;

namespace SmartParking.Api.Endpoints;

public static class ParkingEndpoints
{
    private static IQueryable<ParkingListItemDto> ProjectToDto(IQueryable<Parking> query) =>
        query.Select(p => new ParkingListItemDto
        {
            ParkId = p.ParkId,
            FloorId = p.FloorId,
            CustName = p.CustName,
            CustMobileNo = p.CustMobileNo,
            VehicleNo = p.VehicleNo,
            ParkDate = p.ParkDate,
            ParkSpotNo = p.ParkSpotNo,
            InTime = p.InTime,
            OutTime = p.OutTime,
            Amount = (double)p.Amount,
            ExtraCharge = (double)p.ExtraCharge,
            ParkingNo = p.ParkingNo,
            FloorNo = p.Floor.FloorNo,
            BuildingName = p.Floor.Building.BuildingName,
            SiteName = p.Floor.Building.Site.SiteName,
            ClientName = p.Floor.Building.Site.Client.ClientName
        });

    public static RouteGroupBuilder MapParkingEndpoints(this RouteGroupBuilder group, string namePrefix = "")
    {
        // 24. GET /api/SmartParking/GetAllParking
        group.MapGet("/GetAllParking", async (
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var parkings = await ProjectToDto(
                    db.Parkings.AsNoTracking().OrderByDescending(p => p.ParkId))
                .Take(500)
                .ToListAsync(ct);

            return Results.Ok(ApiResponse.Success(parkings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllParking")
        .WithSummary("Get all parking tickets joined with hierarchy details");

        // 25. GET /api/SmartParking/GetAllActiveParking
        group.MapGet("/GetAllActiveParking", async (
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var activeParkings = await cache.GetOrCreateAsync(
                "parkings:active",
                async cancel =>
                {
                    var list = new List<ParkingListItemDto>();
                    await foreach (var item in SmartParkingCompiledQueries.GetAllActiveParkingsAsync(db).WithCancellation(cancel))
                    {
                        list.Add(item);
                    }
                    return list;
                },
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromSeconds(30),
                    LocalCacheExpiration = TimeSpan.FromSeconds(15)
                },
                tags: [CacheTags.ParkingsActive],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(activeParkings));
        })
        .CacheOutput(p => p.Expire(TimeSpan.FromSeconds(15)).Tag(CacheTags.ParkingsActive))
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllActiveParking")
        .WithSummary("Get all currently active parking sessions (OutTime IS NULL)");

        // 26. GET /api/SmartParking/GetAllParkingByClientId
        group.MapGet("/GetAllParkingByClientId", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var parkings = await ProjectToDto(
                    db.Parkings.AsNoTracking()
                        .Where(p => p.Floor.Building.Site.ClientId == id)
                        .OrderByDescending(p => p.ParkId))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse.Success(parkings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllParkingByClientId")
        .WithSummary("Get all parking sessions for a specific clientId");

        // 27. GET /api/SmartParking/GetAllParkingBySite
        group.MapGet("/GetAllParkingBySite", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var parkings = await ProjectToDto(
                    db.Parkings.AsNoTracking()
                        .Where(p => p.Floor.Building.SiteId == id)
                        .OrderByDescending(p => p.ParkId))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse.Success(parkings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllParkingBySite")
        .WithSummary("Get all parking sessions for a specific siteId");

        // 28. GET /api/SmartParking/GetAllParkingByBuilding
        group.MapGet("/GetAllParkingByBuilding", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var parkings = await ProjectToDto(
                    db.Parkings.AsNoTracking()
                        .Where(p => p.Floor.BuildingId == id)
                        .OrderByDescending(p => p.ParkId))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse.Success(parkings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllParkingByBuilding")
        .WithSummary("Get all parking sessions for a specific buildingId");

        // 29. GET /api/SmartParking/GetAllParkingByFloor
        group.MapGet("/GetAllParkingByFloor", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var parkings = await ProjectToDto(
                    db.Parkings.AsNoTracking()
                        .Where(p => p.FloorId == id)
                        .OrderByDescending(p => p.ParkId))
                .ToListAsync(ct);

            return Results.Ok(ApiResponse.Success(parkings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllParkingByFloor")
        .WithSummary("Get all parking sessions for a specific floorId");

        // 30. POST /api/SmartParking/AddParking
        group.MapPost("/AddParking", async (
            [FromBody] GetVParkings request,
            SmartParkingDbContext db,
            IFeatureManagerSnapshot featureManager,
            ResiliencePipelineProvider<string> pipelineProvider,
            SmartParkingMetrics metrics,
            CacheEvictionService cacheEviction,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            using var activity = SmartParkingMetrics.ActivitySource.StartActivity("Parking.CheckIn");
            activity?.SetTag("parking.floor_id", request.FloorId);
            activity?.SetTag("parking.spot_no", request.ParkSpotNo);
            activity?.SetTag("parking.vehicle_no", request.VehicleNo);

            var logger = loggerFactory.CreateLogger("SmartParking.Parkings");
            var pipeline = pipelineProvider.GetPipeline(DependencyInjection.ParkingCheckInResiliencePipeline);

            var strictValidation = await featureManager.IsEnabledAsync(FeatureFlags.EnableStrictSpotValidation);
            var surgePricingEnabled = await featureManager.IsEnabledAsync(FeatureFlags.EnableDynamicSurgePricing);

            var result = await pipeline.ExecuteAsync(async token =>
            {
                var floor = await db.Floors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FloorId == request.FloorId, token)
                    ?? throw new EntityNotFoundException(nameof(Floor), request.FloorId);

                if (strictValidation)
                {
                    if (!floor.IsOperational)
                    {
                        throw new ParkingConflictException($"Floor '{floor.FloorNo}' is currently not operational.");
                    }

                    if (request.ParkSpotNo > floor.TotalParkingSpots)
                    {
                        throw new ParkingConflictException(
                            $"Spot #{request.ParkSpotNo} exceeds floor capacity ({floor.TotalParkingSpots} spots).");
                    }

                    if (request.ParkSpotNo > 0)
                    {
                        var spotOccupied = await db.Parkings
                            .AsNoTracking()
                            .AnyAsync(p => p.FloorId == request.FloorId &&
                                           p.ParkSpotNo == request.ParkSpotNo &&
                                           p.OutTime == null, token);
                        if (spotOccupied)
                        {
                            throw new ParkingConflictException(
                                $"Spot #{request.ParkSpotNo} on floor '{floor.FloorNo}' is currently occupied.");
                        }
                    }
                }

                var finalAmount = (decimal)request.Amount;
                var surgeApplied = false;

                if (surgePricingEnabled && floor.TotalParkingSpots > 0 && finalAmount > 0)
                {
                    var activeCount = await db.Parkings
                        .AsNoTracking()
                        .CountAsync(p => p.FloorId == request.FloorId && p.OutTime == null, token);

                    var occupancyRatio = (double)activeCount / floor.TotalParkingSpots;
                    if (occupancyRatio >= 0.80)
                    {
                        finalAmount = Math.Round(finalAmount * 1.25m, 2);
                        surgeApplied = true;
                        activity?.SetTag("parking.surge_applied", true);
                    }
                }

                var now = DateTime.UtcNow;
                var parking = new Parking
                {
                    FloorId = request.FloorId,
                    CustName = request.CustName?.Trim(),
                    CustMobileNo = request.CustMobileNo?.Trim(),
                    VehicleNo = request.VehicleNo!.Trim().ToUpperInvariant(),
                    ParkDate = request.ParkDate == default ? now.Date : request.ParkDate,
                    ParkSpotNo = request.ParkSpotNo,
                    InTime = request.InTime == default ? now : request.InTime,
                    OutTime = request.OutTime,
                    Amount = finalAmount,
                    ExtraCharge = (decimal)request.ExtraCharge,
                    ParkingNo = string.IsNullOrWhiteSpace(request.ParkingNo)
                        ? $"PK-{now:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}"
                        : request.ParkingNo.Trim()
                };

                db.Parkings.Add(parking);
                await db.SaveChangesAsync(token);

                logger.LogVehicleCheckedIn(
                    parking.ParkId,
                    parking.VehicleNo,
                    parking.FloorId,
                    parking.ParkSpotNo,
                    parking.Amount,
                    surgeApplied);

                return parking;
            }, ct);

            metrics.RecordCheckIn(result.FloorId, result.ParkSpotNo, (double)result.Amount);
            await cacheEviction.EvictByTagsAsync([CacheTags.ParkingsActive], ct);

            request.ParkId = result.ParkId;
            request.Amount = (double)result.Amount;
            request.ParkingNo = result.ParkingNo;
            request.InTime = result.InTime;
            request.ParkDate = result.ParkDate;

            return Results.Ok(ApiResponse.Success(request, "Vehicle parked successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetVParkings>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName($"{namePrefix}AddParking")
        .WithSummary("Check in a vehicle to a parking spot");

        // 31. POST /api/SmartParking/MarExit (plus /MarkExit alias)
        Func<ExitParking, SmartParkingDbContext, SmartParkingMetrics, CacheEvictionService, ILoggerFactory, CancellationToken, Task<IResult>> handleExit =
            async ([FromBody] ExitParking request,
                   SmartParkingDbContext db,
                   SmartParkingMetrics metrics,
                   CacheEvictionService cacheEviction,
                   ILoggerFactory loggerFactory,
                   CancellationToken ct) =>
            {
                using var activity = SmartParkingMetrics.ActivitySource.StartActivity("Parking.CheckOut");
                activity?.SetTag("parking.park_id", request.ParkId);

                var logger = loggerFactory.CreateLogger("SmartParking.Parkings");
                var parking = await db.Parkings.FirstOrDefaultAsync(p => p.ParkId == request.ParkId, ct)
                    ?? throw new EntityNotFoundException(nameof(Parking), request.ParkId);

                if (parking.OutTime is not null)
                {
                    throw new ParkingConflictException($"Parking ticket #{request.ParkId} has already exited at {parking.OutTime:O}.");
                }

                parking.OutTime = request.OutTime == default ? DateTime.UtcNow : request.OutTime;
                parking.ExtraCharge = (decimal)request.ExtraCharge;

                await db.SaveChangesAsync(ct);

                var durationMinutes = (parking.OutTime.Value - parking.InTime).TotalMinutes;
                metrics.RecordCheckOut(parking.FloorId, durationMinutes, request.ExtraCharge);
                logger.LogVehicleExited(parking.ParkId, parking.VehicleNo, parking.FloorId, durationMinutes, parking.ExtraCharge);

                await cacheEviction.EvictByTagsAsync([CacheTags.ParkingsActive], ct);

                return Results.Ok(ApiResponse.Success(new
                {
                    parking.ParkId,
                    parking.VehicleNo,
                    parking.InTime,
                    parking.OutTime,
                    DurationMinutes = Math.Round(durationMinutes, 2),
                    TotalAmount = parking.Amount + parking.ExtraCharge
                }, "Vehicle exit marked successfully"));
            };

        group.MapPost("/MarExit", handleExit)
            .AddEndpointFilter<ValidationFilter<ExitParking>>()
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName($"{namePrefix}MarExit")
            .WithSummary("Mark vehicle exit and release parking spot (official spec route)");

        group.MapPost("/MarkExit", handleExit)
            .AddEndpointFilter<ValidationFilter<ExitParking>>()
            .RequireRateLimiting(RateLimitPolicies.Write)
            .WithName($"{namePrefix}MarkExit")
            .WithSummary("Alias for /MarExit");

        // 32. POST /api/SmartParking/DeleteParking
        group.MapPost("/DeleteParking", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var deleted = await db.Parkings.Where(p => p.ParkId == id).ExecuteDeleteAsync(ct);
            if (deleted == 0)
            {
                throw new EntityNotFoundException(nameof(Parking), id);
            }

            await cacheEviction.EvictByTagsAsync([CacheTags.ParkingsActive], ct);
            return Results.Ok(ApiResponse.Success(null, "Parking record deleted successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName($"{namePrefix}DeleteParking")
        .WithSummary("Delete a parking ticket by parkId");

        // 33. POST /api/SmartParking/GetAdminAllParkings
        group.MapPost("/GetAdminAllParkings", async (
            [FromBody] AllParkingSearch filter,
            SmartParkingDbContext db,
            IFeatureManagerSnapshot featureManager,
            CancellationToken ct) =>
        {
            var query = db.Parkings.AsNoTracking();

            if (filter.FloorId > 0)
            {
                query = query.Where(p => p.FloorId == filter.FloorId);
            }
            if (filter.BuildingId > 0)
            {
                query = query.Where(p => p.Floor.BuildingId == filter.BuildingId);
            }
            if (filter.SiteId > 0)
            {
                query = query.Where(p => p.Floor.Building.SiteId == filter.SiteId);
            }
            if (filter.ClientId > 0)
            {
                query = query.Where(p => p.Floor.Building.Site.ClientId == filter.ClientId);
            }
            if (filter.FromDate.HasValue && filter.FromDate.Value.Year > 1900)
            {
                query = query.Where(p => p.ParkDate >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue && filter.ToDate.Value.Year > 1900)
            {
                query = query.Where(p => p.ParkDate <= filter.ToDate.Value);
            }
            if (!string.IsNullOrWhiteSpace(filter.VehicleNo) && filter.VehicleNo != "string")
            {
                var vNo = filter.VehicleNo.Trim();
                query = query.Where(p => p.VehicleNo.Contains(vNo));
            }

            var results = await ProjectToDto(query.OrderByDescending(p => p.ParkDate).ThenByDescending(p => p.ParkId))
                .Take(500)
                .ToListAsync(ct);

            var detailedAnalytics = await featureManager.IsEnabledAsync(FeatureFlags.EnableDetailedAdminAnalytics);
            if (detailedAnalytics)
            {
                var totalRevenue = results.Sum(r => r.Amount + r.ExtraCharge);
                var activeCount = results.Count(r => r.OutTime == null);
                return Results.Ok(new ApiResponse
                {
                    Result = true,
                    Message = $"Found {results.Count} records (Active: {activeCount}, Total Revenue: {totalRevenue:F2})",
                    Data = results
                });
            }

            return Results.Ok(ApiResponse.Success(results));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAdminAllParkings")
        .WithSummary("Admin dynamic multi-criteria search across all parkings");

        return group;
    }
}
