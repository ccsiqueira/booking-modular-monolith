namespace Flight.Aircrafts.Events;

using BuildingBlocks.Core.Event;
using ValueObjects;

public record AircraftTelemetryUpdatedDomainEvent(
    Guid AircraftId,
    Position Position,
    Attitude Attitude,
    TelemetryData TelemetryData,
    DateTime Timestamp
) : IDomainEvent
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public long? CreatedBy { get; init; }
    public bool IsDeleted { get; init; } = false;
};