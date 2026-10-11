using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartParking.Api.Infrastructure;
using SmartParking.Application.Common;
using SmartParking.Application.Contracts;
using SmartParking.Application.Observability;
using SmartParking.Domain.Entities;
using SmartParking.Domain.Exceptions;
using SmartParking.Infrastructure.Persistence;

namespace SmartParking.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // 1. GET /api/SmartParking/getAllUsersByClientId
        group.MapGet("/getAllUsersByClientId", async (
            [FromQuery] int clientId,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var query = db.Users.AsNoTracking();
            if (clientId > 0)
            {
                query = query.Where(u => u.ClientId == clientId);
            }

            var users = await query
                .OrderByDescending(u => u.UserId)
                .Select(u => new UserMasterView
                {
                    UserId = u.UserId,
                    EmailId = u.EmailId,
                    Password = "***",
                    FullName = u.FullName,
                    MobileNo = u.MobileNo
                })
                .ToListAsync(ct);

            return Results.Ok(users);
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetAllUsersByClientId")
        .WithSummary("Get all users filtered by clientId");

        // 2. GET /api/SmartParking/getUserById
        group.MapGet("/getUserById", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new UserMasterNew
                {
                    UserId = u.UserId,
                    EmailId = u.EmailId,
                    Password = "***",
                    CreatedDate = u.CreatedDate,
                    ProjectName = u.ProjectName,
                    FullName = u.FullName,
                    MobileNo = u.MobileNo,
                    ExtraId = u.ExtraId ?? u.ClientId
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new EntityNotFoundException(nameof(User), id);

            return Results.Ok(user);
        })
        .RequireRateLimiting(RateLimitPolicies.Read)
        .WithName("GetUserById")
        .WithSummary("Get a single user by userId");

        // 3. POST /api/SmartParking/register
        group.MapPost("/register", async (
            [FromBody] UserMasterViewClient request,
            SmartParkingDbContext db,
            IPasswordHasher<User> passwordHasher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("SmartParking.Users");
            var normalizedEmail = request.EmailId.Trim().ToLowerInvariant();

            var exists = await db.Users.AsNoTracking().AnyAsync(u => u.EmailId == normalizedEmail, ct);
            if (exists)
            {
                throw new ParkingConflictException($"User with email '{normalizedEmail}' is already registered.");
            }

            var user = new User
            {
                ClientId = request.ClientId > 0 ? request.ClientId : null,
                EmailId = normalizedEmail,
                FullName = request.FullName.Trim(),
                MobileNo = request.MobileNo.Trim(),
                ProjectName = "SmartParking",
                ExtraId = request.ClientId > 0 ? request.ClientId : null,
                CreatedDate = DateTime.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            logger.LogUserRegistered(user.UserId, user.EmailId, user.ClientId);

            return Results.Ok(new UserMasterNew
            {
                UserId = user.UserId,
                EmailId = user.EmailId,
                Password = "***",
                CreatedDate = user.CreatedDate,
                ProjectName = user.ProjectName,
                FullName = user.FullName,
                MobileNo = user.MobileNo,
                ExtraId = user.ExtraId
            });
        })
        .AddEndpointFilter<ValidationFilter<UserMasterViewClient>>()
        .RequireRateLimiting(RateLimitPolicies.Auth)
        .WithName("RegisterUser")
        .WithSummary("Register a new user account");

        // 4. POST /api/SmartParking/login
        group.MapPost("/login", async (
            [FromBody] UserLogin request,
            SmartParkingDbContext db,
            IPasswordHasher<User> passwordHasher,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("SmartParking.Users");
            var normalizedEmail = request.EmailId.Trim().ToLowerInvariant();

            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.EmailId == normalizedEmail, ct);

            if (user is null)
            {
                logger.LogFailedLogin(normalizedEmail);
                throw new InvalidCredentialsException();
            }

            var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
            {
                logger.LogFailedLogin(normalizedEmail);
                throw new InvalidCredentialsException();
            }

            return Results.Ok(new UserMasterNew
            {
                UserId = user.UserId,
                EmailId = user.EmailId,
                Password = "***",
                CreatedDate = user.CreatedDate,
                ProjectName = user.ProjectName,
                FullName = user.FullName,
                MobileNo = user.MobileNo,
                ExtraId = user.ExtraId ?? user.ClientId
            });
        })
        .AddEndpointFilter<ValidationFilter<UserLogin>>()
        .RequireRateLimiting(RateLimitPolicies.Auth)
        .WithName("LoginUser")
        .WithSummary("Authenticate user credentials");

        // 5. PUT /api/SmartParking/updateUser
        group.MapPut("/updateUser", async (
            [FromQuery] int id,
            [FromBody] UserMasterNew request,
            SmartParkingDbContext db,
            IPasswordHasher<User> passwordHasher,
            CancellationToken ct) =>
        {
            var targetId = id > 0 ? id : request.UserId;
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == targetId, ct)
                ?? throw new EntityNotFoundException(nameof(User), targetId);

            user.EmailId = request.EmailId.Trim().ToLowerInvariant();
            user.FullName = request.FullName.Trim();
            user.MobileNo = request.MobileNo.Trim();
            user.ProjectName = request.ProjectName ?? user.ProjectName;
            user.ExtraId = request.ExtraId ?? user.ExtraId;

            if (!string.IsNullOrWhiteSpace(request.Password) && request.Password != "***")
            {
                user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse.Success(new UserMasterNew
            {
                UserId = user.UserId,
                EmailId = user.EmailId,
                Password = "***",
                CreatedDate = user.CreatedDate,
                ProjectName = user.ProjectName,
                FullName = user.FullName,
                MobileNo = user.MobileNo,
                ExtraId = user.ExtraId
            }, "User updated successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("UpdateUser")
        .WithSummary("Update an existing user");

        // 6. DELETE /api/SmartParking/deleteUserById
        group.MapDelete("/deleteUserById", async (
            [FromQuery] int id,
            SmartParkingDbContext db,
            CancellationToken ct) =>
        {
            var deleted = await db.Users.Where(u => u.UserId == id).ExecuteDeleteAsync(ct);
            if (deleted == 0)
            {
                throw new EntityNotFoundException(nameof(User), id);
            }

            return Results.Ok(ApiResponse.Success(null, "User deleted successfully"));
        })
        .RequireRateLimiting(RateLimitPolicies.Write)
        .WithName("DeleteUserById")
        .WithSummary("Delete user by userId");

        return group;
    }
}
