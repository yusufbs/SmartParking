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

public static class SiteEndpoints
{
    public static RouteGroupBuilder MapSiteEndpoints(this RouteGroupBuilder group)
    {
        // 11. GET /api/SmartParking/GetAllSites
        group.MapGet("/GetAllSites", async (
            [FromQuery] int? clientid,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var filterClientId = clientid.GetValueOrDefault();
            var sites = await cache.GetOrCreateAsync(
                $"sites:all:{filterClientId}",
                async cancel =>
                {
                    var query = db.Sites.AsNoTracking();
                    if (filterClientId > 0)
                    {
                        query = query.Where(s => s.ClientId == filterClientId);
                    }

                    return await query
                        .OrderByDescending(s => s.SiteId)
                        .Select(s => new SiteListItemDto
                        {
                            SiteId = s.SiteId,
                            ClientId = s.ClientId,
                            ClientName = s.Client.ClientName,
                            SiteName = s.SiteName,
                            SiteCity = s.SiteCity,
                            SiteAddress = s.SiteAddress,
                            SitePinCode = s.SitePinCode,
                            TotalBuildings = s.TotalBuildings,
                            CreatedDate = s.CreatedDate
                        })
                        .ToListAsync(cancel);
                },
                tags: [CacheTags.Sites, CacheTags.ClientSites(filterClientId)],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(sites));
        })
        .CacheOutput(p => p.Expire(TimeSpan.FromMinutes(2)).Tag(CacheTags.Sites))
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetAllSites")
        .WithSummary("Get all sites optionally filtered by clientid");

        // 12. GET /api/SmartParking/GetSitesByClientId
        group.MapGet("/GetSitesByClientId", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var sites = await cache.GetOrCreateAsync(
                $"sites:by-client:{id}",
                async cancel => await db.Sites
                    .AsNoTracking()
                    .Where(s => s.ClientId == id)
                    .OrderByDescending(s => s.SiteId)
                    .Select(s => new SiteListItemDto
                    {
                        SiteId = s.SiteId,
                        ClientId = s.ClientId,
                        SiteName = s.SiteName,
                        SiteCity = s.SiteCity,
                        SiteAddress = s.SiteAddress,
                        SitePinCode = s.SitePinCode,
                        TotalBuildings = s.TotalBuildings,
                        CreatedDate = s.CreatedDate
                    })
                    .ToListAsync(cancel),
                tags: [CacheTags.Sites, CacheTags.ClientSites(id)],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(sites));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetSitesByClientId")
        .WithSummary("Get sites belonging to a specific clientId");

        // 13. GET /api/SmartParking/GetSiteById
        group.MapGet("/GetSiteById", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var site = await cache.GetOrCreateAsync(
                $"sites:by-id:{id}",
                async cancel => await db.Sites
                    .AsNoTracking()
                    .Where(s => s.SiteId == id)
                    .Select(s => new GetVSiteMasterView
                    {
                        SiteId = s.SiteId,
                        ClientId = s.ClientId,
                        SiteName = s.SiteName,
                        SiteCity = s.SiteCity,
                        SiteAddress = s.SiteAddress,
                        SitePinCode = s.SitePinCode,
                        TotalBuildings = s.TotalBuildings,
                        CreatedDate = s.CreatedDate,
                        GetVBuildingss = s.Buildings
                            .Select(b => new GetVBuildings
                            {
                                BuildingId = b.BuildingId,
                                SiteId = b.SiteId,
                                BuildingName = b.BuildingName,
                                BuildingManagerName = b.BuildingManagerName,
                                ContactNo = b.ContactNo
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync(cancel),
                tags: [CacheTags.Sites, CacheTags.SiteById(id)],
                cancellationToken: ct)
                ?? throw new EntityNotFoundException(nameof(Site), id);

            return Results.Ok(ApiResponse.Success(site));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetSiteById")
        .WithSummary("Get a single site with its nested buildings (getVBuildingss)");

        // 14. POST /api/SmartParking/AddClientSite
        group.MapPost("/AddClientSite", async (
            [FromBody] GetVSiteMasterView request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var buildings = request.GetVBuildingss?.Select(b => new Building
            {
                BuildingName = b.BuildingName?.Trim() ?? "Main Building",
                BuildingManagerName = b.BuildingManagerName?.Trim(),
                ContactNo = b.ContactNo?.Trim()
            }).ToList() ?? [];

            var site = new Site
            {
                ClientId = request.ClientId,
                SiteName = request.SiteName!.Trim(),
                SiteCity = request.SiteCity?.Trim(),
                SiteAddress = request.SiteAddress?.Trim(),
                SitePinCode = request.SitePinCode?.Trim(),
                TotalBuildings = buildings.Count > 0 ? buildings.Count : request.TotalBuildings,
                CreatedDate = request.CreatedDate ?? DateTime.UtcNow,
                Buildings = buildings
            };

            db.Sites.Add(site);
            await db.SaveChangesAsync(ct);

            request.SiteId = site.SiteId;
            request.CreatedDate = site.CreatedDate;
            request.GetVBuildingss = site.Buildings.Select(b => new GetVBuildings
            {
                BuildingId = b.BuildingId,
                SiteId = site.SiteId,
                BuildingName = b.BuildingName,
                BuildingManagerName = b.BuildingManagerName,
                ContactNo = b.ContactNo
            }).ToList();

            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Sites, CacheTags.Buildings, CacheTags.ClientSites(site.ClientId)], ct);

            return Results.Ok(ApiResponse.Success(request, "Site and buildings added successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetVSiteMasterView>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("AddClientSite")
        .WithSummary("Create a client site along with nested buildings");

        // 15. POST /api/SmartParking/UpdateSite
        group.MapPost("/UpdateSite", async (
            [FromBody] GetVSiteMasterView request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var site = await db.Sites
                .Include(s => s.Buildings)
                .FirstOrDefaultAsync(s => s.SiteId == request.SiteId, ct)
                ?? throw new EntityNotFoundException(nameof(Site), request.SiteId);

            site.ClientId = request.ClientId;
            site.SiteName = request.SiteName!.Trim();
            site.SiteCity = request.SiteCity?.Trim();
            site.SiteAddress = request.SiteAddress?.Trim();
            site.SitePinCode = request.SitePinCode?.Trim();
            site.TotalBuildings = request.TotalBuildings;

            if (request.GetVBuildingss is not null)
            {
                foreach (var bDto in request.GetVBuildingss)
                {
                    if (bDto.BuildingId > 0)
                    {
                        var existing = site.Buildings.FirstOrDefault(x => x.BuildingId == bDto.BuildingId);
                        if (existing is not null)
                        {
                            existing.BuildingName = bDto.BuildingName?.Trim() ?? existing.BuildingName;
                            existing.BuildingManagerName = bDto.BuildingManagerName?.Trim();
                            existing.ContactNo = bDto.ContactNo?.Trim();
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(bDto.BuildingName))
                    {
                        site.Buildings.Add(new Building
                        {
                            SiteId = site.SiteId,
                            BuildingName = bDto.BuildingName.Trim(),
                            BuildingManagerName = bDto.BuildingManagerName?.Trim(),
                            ContactNo = bDto.ContactNo?.Trim()
                        });
                    }
                }
                site.TotalBuildings = site.Buildings.Count;
            }

            await db.SaveChangesAsync(ct);
            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Sites, CacheTags.Buildings, CacheTags.SiteById(site.SiteId), CacheTags.ClientSites(site.ClientId)], ct);

            return Results.Ok(ApiResponse.Success(request, "Site updated successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetVSiteMasterView>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("UpdateSite")
        .WithSummary("Update site details and synchronize nested buildings");

        // 16. POST /api/SmartParking/DeleteSite
        group.MapPost("/DeleteSite", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var site = await db.Sites.FirstOrDefaultAsync(s => s.SiteId == id, ct)
                ?? throw new EntityNotFoundException(nameof(Site), id);

            db.Sites.Remove(site);
            await db.SaveChangesAsync(ct);

            await cacheEviction.EvictByTagsAsync(
                [CacheTags.Sites, CacheTags.Buildings, CacheTags.SiteById(id), CacheTags.ClientSites(site.ClientId)], ct);

            return Results.Ok(ApiResponse.Success(null, "Site deleted successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("DeleteSite")
        .WithSummary("Delete a site by siteId");

        // 17. GET /api/SmartParking/GetBuildingBySiteId
        group.MapGet("/GetBuildingBySiteId", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var buildings = await cache.GetOrCreateAsync(
                $"buildings:by-site:{id}",
                async cancel => await db.Buildings
                    .AsNoTracking()
                    .Where(b => b.SiteId == id)
                    .OrderBy(b => b.BuildingId)
                    .Select(b => new GetVBuildings
                    {
                        BuildingId = b.BuildingId,
                        SiteId = b.SiteId,
                        BuildingName = b.BuildingName,
                        BuildingManagerName = b.BuildingManagerName,
                        ContactNo = b.ContactNo
                    })
                    .ToListAsync(cancel),
                tags: [CacheTags.Buildings, CacheTags.SiteById(id)],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(buildings));
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetBuildingBySiteId")
        .WithSummary("Get all buildings for a given siteId");

        return group;
    }
}
