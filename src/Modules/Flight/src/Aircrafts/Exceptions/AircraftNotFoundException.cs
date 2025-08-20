using BuildingBlocks.Exception;

namespace Flight.Aircrafts.Exceptions;

public class AircraftNotFoundException : NotFoundException
{
    public AircraftNotFoundException(Guid aircraftId)
        : base($"Aircraft with ID '{aircraftId}' was not found.")
    {
    }
}