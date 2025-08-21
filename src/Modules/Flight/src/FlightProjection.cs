using BuildingBlocks.EventStoreDB.Events;
using BuildingBlocks.EventStoreDB.Projections;
using Flight.Data;
using MediatR;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Flight;

using Flight.Aircrafts.Events;
using Flight.Aircrafts.Features.CreatingAircraft.V1;
using Flight.Aircrafts.Models;
using MassTransit;

public class FlightProjection : IProjectionProcessor
{
    private readonly FlightReadDbContext _flightReadDbContext;

    public FlightProjection(FlightReadDbContext flightReadDbContext)
    {
        _flightReadDbContext = flightReadDbContext;
    }

    public async Task ProcessEventAsync<T>(StreamEvent<T> streamEvent, CancellationToken cancellationToken = default)
        where T : INotification
    {
        switch (streamEvent.Data)
        {
            case AircraftCreatedDomainEvent aircraftCreatedDomainEvent:
                await Apply(aircraftCreatedDomainEvent, cancellationToken);
                break;
            // AircraftTelemetryUpdatedDomainEvent is now handled by UpdateAircraftTelemetryMongo internal command
            // No need to handle it here as it goes directly to MongoDB via internal command
        }
    }

    private async Task Apply(AircraftCreatedDomainEvent @event, CancellationToken cancellationToken = default)
    {
        var aircraft = await _flightReadDbContext.Aircraft.AsQueryable()
            .SingleOrDefaultAsync(x => x.AircraftId == @event.Id && !x.IsDeleted, cancellationToken);

        if (aircraft == null)
        {
            var aircraftReadModel = new AircraftReadModel
            {
                Id = NewId.NextGuid(),
                AircraftId = @event.Id,
                Name = @event.Name,
                Model = @event.Model,
                ManufacturingYear = @event.ManufacturingYear,
                IsDeleted = @event.IsDeleted,
                // Initialize telemetry fields
                Latitude = null,
                Longitude = null,
                Altitude = null,
                Speed = null,
                Heading = null,
                FuelLevel = null,
                FlightPhase = null,
                LastTelemetryUpdate = null
            };

            await _flightReadDbContext.Aircraft.InsertOneAsync(aircraftReadModel, cancellationToken: cancellationToken);
        }
    }
}