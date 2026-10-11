using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using SmartParking.Api.Infrastructure;
using SmartParking.Application.Common;
using SmartParking.Application.Contracts;
using SmartParking.Domain.Entities;
using SmartParking.Domain.Exceptions;
using SmartParking.Infrastructure.Persistence;

namespace SmartParking.Api.Endpoints;

public static class FloorEndpoints
{
    public static RouteGroupBuilder MapFloorEndpoints(this RouteGroupBuilder group)
    {
        // 18. GET /api/SmartParking/GetAllFloor
        group.MapGet("/GetAllFloor", async (
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var floors = await cache.GetOrCreateAsync(
                "floors:all",
                async cancel =>
                {
                    var list = new List<FloorListItemDto>();
                    await foreach (var item in SmartParkingCompiledQueries.GetAllFloorsAsync(db).WithCancellation(cancel))
                    {
                        list.Add(item);
                    }
                    return list;
                },
                tags: [CacheTags.Floors],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(floors));
        })
        .CacheOutput(p => p.Expire(TimeSpan.FromMinutes(2)).Tag(CacheTags.Floors))
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetAllFloor")
        .WithSummary("Get all parking floors with buildingName and siteName");

        // 19. GET /api/SmartParking/GetFloorsByBuildingId
        group.MapGet("/GetFloorsByBuildingId", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var floors = await cache.GetOrCreateAsync(
                $"floors:by-building:{id}",
                async cancel => await db.Floors
                    .AsNoTracking()
                    .Where(f => f.BuildingId == id)
                    .OrderBy(f => f.FloorId)
                    .Select(f => new FloorListItemDto
                    {
                        FloorId = f.FloorId,
                        BuildingId = f.BuildingId,
                        FloorNo = f.FloorNo,
                        IsOperational = f.IsOperational,
                        TotalParkingSpots = f.TotalParkingSpots,
                        BuildingName = f.Building.BuildingName,
                        SiteName = f.Building.Site.SiteName
                    })
                    .ToListAsync(cancel),
                tags: [CacheTags.Floors, CacheTags.BuildingFloors(id)],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(floors));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetFloorsByBuildingId")
        .WithSummary("Get all floors belonging to a specific buildingId");

        // 20. GET /api/SmartParking/GetFloorsBySiteId
        group.MapGet("/GetFloorsBySiteId", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var floors = await cache.GetOrCreateAsync(
                $"floors:by-site:{id}",
                async cancel => await db.Floors
                    .AsNoTracking()
                    .Where(f => f.Building.SiteId == id)
                    .OrderBy(f => f.FloorId)
                    .Select(f => new FloorListItemDto
                    {
                        FloorId = f.FloorId,
                        BuildingId = f.BuildingId,
                        FloorNo = f.FloorNo,
                        IsOperational = f.IsOperational,
                        TotalParkingSpots = f.TotalParkingSpots,
                        BuildingName = f.Building.BuildingName,
                        SiteName = f.Building.Site.SiteName
                    })
                    .ToListAsync(cancel),
                tags: [CacheTags.Floors, CacheTags.SiteFloors(id)],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(floors));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetFloorsBySiteId")
        .WithSummary("Get all floors across all buildings for a given siteId");

        // 21. POST /api/SmartParking/AddFloor
        group.MapPost("/AddFloor", async (
            [FromBody] GetVFloors request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var floor = new Floor
            {
                BuildingId = request.BuildingId,
                FloorNo = request.FloorNo!.Trim(),
                IsOperational = request.IsOperational,
                TotalParkingSpots = request.TotalParkingSpots
            };

            db.Floors.Add(floor);
            await db.SaveChangesAsync(ct);

            request.FloorId = floor.FloorId;
            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Floors, CacheTags.BuildingFloors(floor.BuildingId)], ct);

            return Results.Ok(ApiResponse.Success(request, "Floor added successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetVFloors>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("AddFloor")
        .WithSummary("Create a new parking floor");

        // 22. POST /api/SmartParking/UpdateFloor
        group.MapPost("/UpdateFloor", async (
            [FromBody] GetVFloors request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var floor = await db.Floors.FirstOrDefaultAsync(f => f.FloorId == request.FloorId, ct)
                ?? throw new EntityNotFoundException(nameof(Floor), request.FloorId);

            floor.BuildingId = request.BuildingId;
            floor.FloorNo = request.FloorNo!.Trim();
            floor.IsOperational = request.IsOperational;
            floor.TotalParkingSpots = request.TotalParkingSpots;

            await db.SaveChangesAsync(ct);
            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Floors, CacheTags.BuildingFloors(floor.BuildingId)], ct);

            return Results.Ok(ApiResponse.Success(request, "Floor updated successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetVFloors>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("UpdateFloor")
        .WithSummary("Update an existing parking floor");

        // 23. POST /api/SmartParking/DeleteFloor
        group.MapPost("/DeleteFloor", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var floor = await db.Floors.FirstOrDefaultAsync(f => f.FloorId == id, ct)
                ?? throw new EntityNotFoundException(nameof(Floor), id);

            db.Floors.Remove(floor);
            await db.SaveChangesAsync(ct);

            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Floors, CacheTags.BuildingFloors(floor.BuildingId)], ct);

            return Results.Ok(ApiResponse.Success(null, "Floor deleted successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("DeleteFloor")
        .WithSummary("Delete a parking floor by floorId");

        return group;
    }
}
