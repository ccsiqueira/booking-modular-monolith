using BuildingBlocks.EventStoreDB.Events;

namespace Flight.Aircrafts.Models;

using Events;
using Features.CreatingAircraft.V1;
using ValueObjects;

public record Aircraft : AggregateEventSourcing<AircraftId>
{
    // Static aircraft properties
    public Name Name { get; private set; } = default!;
    public Model Model { get; private set; } = default!;
    public ManufacturingYear ManufacturingYear { get; private set; } = default!;

    // Current telemetry state (reconstructed from events)
    public Position? CurrentPosition { get; private set; }
    public Attitude? CurrentAttitude { get; private set; }
    public TelemetryData? CurrentTelemetry { get; private set; }
    public DateTime? LastTelemetryUpdate { get; private set; }

    public static Aircraft Create(AircraftId id, Name name, Model model, ManufacturingYear manufacturingYear, bool isDeleted = false)
    {
        var aircraft = new Aircraft { Id = id, IsDeleted = isDeleted };

        var @event = new AircraftCreatedDomainEvent(
            aircraft.Id,
            name,
            model,
            manufacturingYear,
            isDeleted);

        aircraft.AddDomainEvent(@event);
        aircraft.Apply(@event);

        return aircraft;
    }

    public void UpdateTelemetry(Position position, Attitude attitude, TelemetryData telemetryData)
    {
        var @event = new AircraftTelemetryUpdatedDomainEvent(
            Id.Value,
            position,
            attitude,
            telemetryData,
            DateTime.UtcNow);

        AddDomainEvent(@event);
        Apply(@event);
    }

    public override void When(object @event)
    {
        switch (@event)
        {
            case AircraftCreatedDomainEvent aircraftCreated:
                Apply(aircraftCreated);
                break;
            case AircraftTelemetryUpdatedDomainEvent telemetryUpdated:
                Apply(telemetryUpdated);
                break;
        }
    }

    private void Apply(AircraftCreatedDomainEvent @event)
    {
        Id = AircraftId.Of(@event.Id);
        Name = @event.Name;
        Model = @event.Model;
        ManufacturingYear = @event.ManufacturingYear;
        IsDeleted = @event.IsDeleted;
        Version++;
    }

    private void Apply(AircraftTelemetryUpdatedDomainEvent @event)
    {
        CurrentPosition = @event.Position;
        CurrentAttitude = @event.Attitude;
        CurrentTelemetry = @event.TelemetryData;
        LastTelemetryUpdate = @event.Timestamp;
        Version++;
    }
}