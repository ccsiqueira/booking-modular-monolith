using BuildingBlocks.Exception;
using SmartCharging.Infrastructure.Exceptions;

namespace Flight.Aircrafts.Exceptions;

public class AircraftNotFoundException : AppException
{
    public AircraftNotFoundException(Guid aircraftId)
        : base($"Aircraft with ID '{aircraftId}' was not found.")
    {
    }
}
