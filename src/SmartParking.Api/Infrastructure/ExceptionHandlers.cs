using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;
using SmartParking.Domain.Exceptions;

namespace SmartParking.Api.Infrastructure;

internal static class ExceptionResponseHelper
{
    public static async ValueTask<bool> WriteErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail,
        IDictionary<string, object?>? extensions = null,
        CancellationToken ct = default)
    {
        httpContext.Response.StatusCode = statusCode;
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        // Support legacy frontend clients expecting ApiResponse { result: false, message: "..." }
        var envelopeHeader = httpContext.Request.Headers["X-Response-Envelope"].FirstOrDefault();
        var envelopeQuery = httpContext.Request.Query["envelope"].FirstOrDefault();
        if (string.Equals(envelopeHeader, "ApiResponse", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(envelopeQuery, "apiResponse", StringComparison.OrdinalIgnoreCase))
        {
            await httpContext.Response.WriteAsJsonAsync(
                ApiResponse.Failure(detail, new { traceId, statusCode, errors = extensions }),
                ct);
            return true;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        if (extensions is not null)
        {
            foreach (var kvp in extensions)
            {
                problem.Extensions[kvp.Key] = kvp.Value;
            }
        }

        await httpContext.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}

public sealed class ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        logger.LogWarning("Validation failed on {Path}: {ErrorCount} error(s)",
            httpContext.Request.Path, validationException.Errors.Count());

        var errors = validationException.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());

        return await ExceptionResponseHelper.WriteErrorAsync(
            httpContext,
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            "Please check the request payload for invalid fields.",
            new Dictionary<string, object?> { ["errors"] = errors },
            ct);
    }
}

public sealed class ParkingConflictExceptionHandler(ILogger<ParkingConflictExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is ParkingConflictException conflictEx)
        {
            logger.LogWarning("Parking domain conflict on {Path}: {Message}", httpContext.Request.Path, conflictEx.Message);
            return await ExceptionResponseHelper.WriteErrorAsync(
                httpContext,
                StatusCodes.Status409Conflict,
                "Parking Spot Conflict",
                conflictEx.Message,
                ct: ct);
        }

        if (exception is DbUpdateConcurrencyException concurrencyEx)
        {
            logger.LogWarning(concurrencyEx, "Optimistic concurrency conflict on {Path}", httpContext.Request.Path);
            return await ExceptionResponseHelper.WriteErrorAsync(
                httpContext,
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "The record was modified by another request. Please refresh and retry.",
                ct: ct);
        }

        if (exception is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } sqlEx })
        {
            logger.LogWarning(sqlEx, "SQL unique constraint violation ({ErrorNumber}) on {Path}", sqlEx.Number, httpContext.Request.Path);
            return await ExceptionResponseHelper.WriteErrorAsync(
                httpContext,
                StatusCodes.Status409Conflict,
                "Duplicate Resource or Occupied Spot",
                "A record with the same unique key or active parking spot already exists.",
                ct: ct);
        }

        return false;
    }
}

public sealed class EntityNotFoundExceptionHandler(ILogger<EntityNotFoundExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not EntityNotFoundException notFoundEx)
        {
            return false;
        }

        logger.LogInformation("Resource not found on {Path}: {Message}", httpContext.Request.Path, notFoundEx.Message);
        return await ExceptionResponseHelper.WriteErrorAsync(
            httpContext,
            StatusCodes.Status404NotFound,
            "Resource Not Found",
            notFoundEx.Message,
            ct: ct);
    }
}

public sealed class InvalidCredentialsExceptionHandler(ILogger<InvalidCredentialsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not InvalidCredentialsException credEx)
        {
            return false;
        }

        logger.LogWarning("Authentication failure on {Path}: {Message}", httpContext.Request.Path, credEx.Message);
        return await ExceptionResponseHelper.WriteErrorAsync(
            httpContext,
            StatusCodes.Status401Unauthorized,
            "Authentication Failed",
            credEx.Message,
            ct: ct);
    }
}

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var detail = environment.IsDevelopment()
            ? exception.Message
            : "An unexpected server error occurred. Please contact support with the provided traceId.";

        return await ExceptionResponseHelper.WriteErrorAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            detail,
            ct: ct);
    }
}
