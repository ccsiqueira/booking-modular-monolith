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
            case AircraftTelemetryUpdatedDomainEvent telemetryUpdatedDomainEvent:
                await Apply(telemetryUpdatedDomainEvent, cancellationToken);
                break;
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
                Name = @event.Name.Value,
                Model = @event.Model.Value,
                ManufacturingYear = @event.ManufacturingYear.Value,
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

    private async Task Apply(AircraftTelemetryUpdatedDomainEvent @event, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AircraftReadModel>.Filter.And(
            Builders<AircraftReadModel>.Filter.Eq(x => x.AircraftId, @event.AircraftId),
            Builders<AircraftReadModel>.Filter.Eq(x => x.IsDeleted, false)
        );

        var update = Builders<AircraftReadModel>.Update
            .Set(x => x.Latitude, @event.Position.Latitude)
            .Set(x => x.Longitude, @event.Position.Longitude)
            .Set(x => x.Altitude, @event.Position.Altitude)
            .Set(x => x.Roll, @event.Attitude.Roll)
            .Set(x => x.Pitch, @event.Attitude.Pitch)
            .Set(x => x.Yaw, @event.Attitude.Yaw)
            .Set(x => x.Speed, @event.TelemetryData.Speed)
            .Set(x => x.Heading, @event.TelemetryData.Heading)
            .Set(x => x.FuelLevel, @event.TelemetryData.FuelLevel)
            .Set(x => x.FlightPhase, @event.TelemetryData.FlightPhase)
            .Set(x => x.LastTelemetryUpdate, @event.Timestamp);

        await _flightReadDbContext.Aircraft.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}