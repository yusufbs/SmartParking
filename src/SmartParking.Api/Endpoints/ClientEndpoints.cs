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

public static class ClientEndpoints
{
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder group, string namePrefix = "")
    {
        // 7. GET /api/SmartParking/GetAllClients
        group.MapGet("/GetAllClients", async (
            SmartParkingDbContext db,
            HybridCache cache,
            CancellationToken ct) =>
        {
            var clients = await cache.GetOrCreateAsync(
                "clients:all",
                async cancel =>
                {
                    var list = new List<GetWClients>();
                    await foreach (var item in SmartParkingCompiledQueries.GetAllClientsAsync(db).WithCancellation(cancel))
                    {
                        list.Add(item);
                    }
                    return list;
                },
                tags: [CacheTags.Clients],
                cancellationToken: ct);

            return Results.Ok(ApiResponse.Success(clients));
        })
        .CacheOutput(p => p.Expire(TimeSpan.FromMinutes(2)).Tag(CacheTags.Clients))
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName($"{namePrefix}GetAllClients")
        .WithSummary("Get all registered parking clients");

        // 8. POST /api/SmartParking/AddClient
        group.MapPost("/AddClient", async (
            [FromBody] GetWClients request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var entity = new Client
            {
                ClientName = request.ClientName!.Trim(),
                BusinessName = request.BusinessName?.Trim(),
                ContactPerson = request.ContactPerson?.Trim(),
                ContactNo = request.ContactNo?.Trim(),
                AltContactNo = request.AltContactNo?.Trim(),
                Email = request.Email?.Trim(),
                CreatedDate = request.CreatedDate == default ? DateTime.UtcNow : request.CreatedDate,
                Logo = request.Logo
            };

            db.Clients.Add(entity);
            await db.SaveChangesAsync(ct);

            request.ClientId = entity.ClientId;
            request.CreatedDate = entity.CreatedDate;

            await cacheEviction.EvictByTagsAsync([CacheTags.Clients], ct);

            return Results.Ok(ApiResponse.Success(request, "Client created successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetWClients>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName($"{namePrefix}AddClient")
        .WithSummary("Add a new client organization");

        // 9. POST /api/SmartParking/UpdateClient
        group.MapPost("/UpdateClient", async (
            [FromBody] GetWClients request,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var entity = await db.Clients.FirstOrDefaultAsync(c => c.ClientId == request.ClientId, ct)
                ?? throw new EntityNotFoundException(nameof(Client), request.ClientId);

            entity.ClientName = request.ClientName!.Trim();
            entity.BusinessName = request.BusinessName?.Trim();
            entity.ContactPerson = request.ContactPerson?.Trim();
            entity.ContactNo = request.ContactNo?.Trim();
            entity.AltContactNo = request.AltContactNo?.Trim();
            entity.Email = request.Email?.Trim();
            entity.Logo = request.Logo;

            await db.SaveChangesAsync(ct);
            await cacheEviction.EvictByTagsAsync([CacheTags.Clients, CacheTags.Sites], ct);

            return Results.Ok(ApiResponse.Success(request, "Client updated successfully"));
        })
        .AddEndpointFilter<ValidationFilter<GetWClients>>()
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName($"{namePrefix}UpdateClient")
        .WithSummary("Update an existing client organization");

        // 10. POST /api/SmartParking/DeleteClient
        group.MapPost("/DeleteClient", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CacheEvictionService cacheEviction,
            CancellationToken ct) =>
        {
            var entity = await db.Clients.FirstOrDefaultAsync(c => c.ClientId == id, ct)
                ?? throw new EntityNotFoundException(nameof(Client), id);

            entity.IsDeleted = true;
            await db.SaveChangesAsync(ct);
            await cacheEviction.EvictByTagsAsync([CacheTags.Clients, CacheTags.Sites], ct);

            return Results.Ok(ApiResponse.Success(null, "Client deleted successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName($"{namePrefix}DeleteClient")
        .WithSummary("Soft-delete a client by clientId");

        return group;
    }
}
