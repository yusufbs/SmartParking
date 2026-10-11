namespace SmartParking.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);

public sealed class EntityNotFoundException(string entityName, object key)
    : DomainException($"{entityName} with identifier '{key}' was not found.");

public sealed class ParkingConflictException(string message)
    : DomainException(message);

public sealed class InvalidCredentialsException(string message = "Invalid email or password.")
    : DomainException(message);
