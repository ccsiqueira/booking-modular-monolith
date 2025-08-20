using SmartCharging.Infrastructure.Exceptions;

namespace Flight.Aircrafts.Exceptions;

public class InvalidTelemetryDataException : DomainException
{
    public InvalidTelemetryDataException(string message)
        : base($"Invalid telemetry data: {message}")
    {
    }
}