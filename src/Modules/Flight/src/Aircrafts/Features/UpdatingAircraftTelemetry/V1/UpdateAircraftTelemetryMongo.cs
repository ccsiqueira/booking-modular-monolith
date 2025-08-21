namespace Flight.Aircrafts.Features.UpdatingAircraftTelemetry.V1;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Event;
using Data;
using Dtos;
using Exceptions;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging; // Added for logging
using Models;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using ValueObjects;

public record UpdateAircraftTelemetryMongo(
    Guid AircraftId,
    TelemetryDto Telemetry
) : InternalCommand;

internal class UpdateAircraftTelemetryMongoHandler : ICommandHandler<UpdateAircraftTelemetryMongo>
{
    private readonly FlightReadDbContext _flightReadDbContext;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateAircraftTelemetryMongoHandler> _logger; // Added logger

    public UpdateAircraftTelemetryMongoHandler(
        FlightReadDbContext flightReadDbContext,
        IMapper mapper,
        ILogger<UpdateAircraftTelemetryMongoHandler> logger) // Added logger parameter
    {
        _flightReadDbContext = flightReadDbContext;
        _mapper = mapper;
        _logger = logger; // Assigned logger
    }

    public async Task<Unit> Handle(UpdateAircraftTelemetryMongo request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        var handlerStartTime = DateTime.UtcNow;
        _logger.LogInformation("🔄 UpdateAircraftTelemetryMongo handler started at: {Timestamp} for AircraftId: {AircraftId}",
            handlerStartTime, request.AircraftId);

        _logger.LogInformation("📊 Updating MongoDB with telemetry data:");
        _logger.LogInformation("  - Position: Lat={Lat}, Lon={Lon}, Alt={Alt}",
            request.Telemetry.Latitude, request.Telemetry.Longitude, request.Telemetry.Altitude);
        _logger.LogInformation("  - Attitude: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}",
            request.Telemetry.Roll, request.Telemetry.Pitch, request.Telemetry.Yaw);
        _logger.LogInformation("  - Telemetry: Speed={Speed}, Heading={Heading}, Fuel={Fuel}, Phase={Phase}",
            request.Telemetry.Speed, request.Telemetry.Heading, request.Telemetry.FuelLevel, request.Telemetry.FlightPhase);
        _logger.LogInformation("  - Timestamp: {Timestamp}", request.Telemetry.Timestamp);

        var filter = Builders<AircraftReadModel>.Filter.And(
            Builders<AircraftReadModel>.Filter.Eq(x => x.AircraftId, request.AircraftId),
            Builders<AircraftReadModel>.Filter.Eq(x => x.IsDeleted, false)
        );

        var update = Builders<AircraftReadModel>.Update
            .Set(x => x.Latitude, request.Telemetry.Latitude)
            .Set(x => x.Longitude, request.Telemetry.Longitude)
            .Set(x => x.Altitude, request.Telemetry.Altitude)
            .Set(x => x.Roll, request.Telemetry.Roll)
            .Set(x => x.Pitch, request.Telemetry.Pitch)
            .Set(x => x.Yaw, request.Telemetry.Yaw)
            .Set(x => x.Speed, request.Telemetry.Speed)
            .Set(x => x.Heading, request.Telemetry.Heading)
            .Set(x => x.FuelLevel, request.Telemetry.FuelLevel)
            .Set(x => x.FlightPhase, request.Telemetry.FlightPhase)
            .Set(x => x.LastTelemetryUpdate, request.Telemetry.Timestamp);

        _logger.LogInformation("🔍 Executing MongoDB update with filter: AircraftId={AircraftId}, IsDeleted=false",
            request.AircraftId);

        var result = await _flightReadDbContext.Aircraft.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        var handlerEndTime = DateTime.UtcNow;
        var handlerDuration = handlerEndTime - handlerStartTime;

        if (result.MatchedCount == 0)
        {
            _logger.LogWarning("⚠️ No aircraft found in MongoDB for ID {AircraftId} (MatchedCount: {MatchedCount}, ModifiedCount: {ModifiedCount})",
                request.AircraftId, result.MatchedCount, result.ModifiedCount);
            throw new AircraftNotFoundException(request.AircraftId);
        }

        _logger.LogInformation("✅ MongoDB update completed successfully at: {Timestamp} (Duration: {Duration}ms)",
            handlerEndTime, handlerDuration.TotalMilliseconds);
        _logger.LogInformation("📊 Update result: MatchedCount={MatchedCount}, ModifiedCount={ModifiedCount}",
            result.MatchedCount, result.ModifiedCount);

        return Unit.Value;
    }
}