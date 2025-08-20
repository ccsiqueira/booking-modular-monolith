using SmartCharging.Infrastructure.Exceptions;

namespace Flight.Aircrafts.Exceptions;

public class InvalidPositionException : DomainException
{
    public InvalidPositionException(string message)
        : base($"Invalid position: {message}")
    {
    }
}