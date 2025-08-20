using BuildingBlocks.Core.Model;

namespace Flight.Aircrafts.Models;

using Features.CreatingAircraft.V1;
using ValueObjects;

public record Aircraft : Aggregate<AircraftId>
{
    // Static aircraft properties
    public Name Name { get; private set; } = default!;
    public Model Model { get; private set; } = default!;
    public ManufacturingYear ManufacturingYear { get; private set; } = default!;

    public static Aircraft Create(AircraftId id, Name name, Model model, ManufacturingYear manufacturingYear, bool isDeleted = false)
    {
        var aircraft = new Aircraft 
        { 
            Id = id,
            Name = name,
            Model = model,
            ManufacturingYear = manufacturingYear,
            IsDeleted = isDeleted
        };

        var @event = new AircraftCreatedDomainEvent(
            aircraft.Id.Value,
            aircraft.Name.Value,
            aircraft.Model.Value,
            aircraft.ManufacturingYear.Value,
            isDeleted);

        aircraft.AddDomainEvent(@event);

        return aircraft;
    }
}