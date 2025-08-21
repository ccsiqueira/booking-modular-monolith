namespace Flight.Aircrafts.Features.GettingAircraftTelemetry.V1;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Web;
using Duende.IdentityServer.EntityFramework.Entities;
using Flight.Aircrafts.Exceptions;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using Flight.Data;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Added for logging
using MongoDB.Driver;
using MongoDB.Driver.Linq;

public record GetAircraftTelemetry(AircraftId AircraftId) : IQuery<GetAircraftTelemetryResult>;

public record GetAircraftTelemetryResult(AircraftTelemetryResponse TelemetryData);

public record AircraftTelemetryResponse
{
    public Guid AircraftId { get; init; }
    public string Name { get; init; } = default!;
    public string Model { get; init; } = default!;
    public int ManufacturingYear { get; init; }
    public PositionDto? Position { get; init; }
    public AttitudeDto? Attitude { get; init; }
    public TelemetryDto? Telemetry { get; init; }
    public DateTime? LastUpdate { get; init; }
    public bool HasTelemetry { get; init; }
}

public record PositionDto
{
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public double Altitude { get; init; }
}

public record AttitudeDto
{
    public double Roll { get; init; }
    public double Pitch { get; init; }
    public double Yaw { get; init; }
}

public record TelemetryDto
{
    public double Speed { get; init; }
    public double Heading { get; init; }
    public double FuelLevel { get; init; }
    public string FlightPhase { get; init; } = default!;
}

public class GetAircraftTelemetryEndpoint : IMinimalEndpoint
{
    public IEndpointRouteBuilder MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet($"{EndpointConfig.BaseApiPath}/flight/aircraft/{{aircraftId:guid}}/telemetry",
                async (Guid aircraftId, IMediator mediator, IMapper mapper, CancellationToken cancellationToken) =>
            {
                var query = new GetAircraftTelemetry(AircraftId.Of(aircraftId));
                var result = await mediator.Send(query, cancellationToken);
                return Results.Ok(result.TelemetryData);
            })
            //.RequireAuthorization(nameof(ApiScope)) // TODO return authentication when ready
            .WithName("GetAircraftTelemetry")
            .WithApiVersionSet(builder.NewApiVersionSet("Flight").Build())
            .Produces<AircraftTelemetryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get current aircraft telemetry frame")
            .WithDescription("Returns the current real-time telemetry data for a specific aircraft including position, attitude, speed, and other flight data. Requires authentication.")
            .WithOpenApi()
            .HasApiVersion(1.0);

        // Debug endpoint to show raw MongoDB data (no caching)
        /*builder.MapGet($"{EndpointConfig.BaseApiPath}/flight/aircraft/{{aircraftId:guid}}/telemetry/debug",
                async (Guid aircraftId, FlightReadDbContext mongoContext, CancellationToken cancellationToken) =>
            {
                var filter = Builders<AircraftReadModel>.Filter.And(
                    Builders<AircraftReadModel>.Filter.Eq(x => x.AircraftId, aircraftId),
                    Builders<AircraftReadModel>.Filter.Eq(x => x.IsDeleted, false)
                );

                var aircraft = await mongoContext.Aircraft.Find(filter).FirstOrDefaultAsync(cancellationToken);
                
                if (aircraft == null)
                {
                    return Results.NotFound($"No aircraft found with ID: {aircraftId}");
                }

                var debugResponse = new
                {
                    AircraftId = aircraft.AircraftId,
                    LastTelemetryUpdate = aircraft.LastTelemetryUpdate,
                    CurrentTime = DateTime.UtcNow,
                    DataAge = aircraft.LastTelemetryUpdate.HasValue ? DateTime.UtcNow - aircraft.LastTelemetryUpdate.Value : TimeSpan.Zero,
                    Position = new
                    {
                        Latitude = aircraft.Latitude,
                        Longitude = aircraft.Longitude,
                        Altitude = aircraft.Altitude
                    },
                    Attitude = new
                    {
                        Roll = aircraft.Roll,
                        Pitch = aircraft.Pitch,
                        Yaw = aircraft.Yaw
                    },
                    Telemetry = new
                    {
                        Speed = aircraft.Speed,
                        Heading = aircraft.Heading,
                        FuelLevel = aircraft.FuelLevel,
                        FlightPhase = aircraft.FlightPhase
                    },
                    RawDocument = aircraft // Include the full MongoDB document for debugging
                };

                return Results.Ok(debugResponse);
            })
            .RequireAuthorization(nameof(ApiScope))
            .WithName("GetAircraftTelemetryDebug")
            .WithSummary("Debug endpoint - Get raw MongoDB telemetry data")
            .WithDescription("Debug endpoint that shows raw MongoDB data without any processing or caching. Useful for troubleshooting telemetry updates.")
            .WithOpenApi()
            .HasApiVersion(1.0);*/

        return builder;
    }
}

public class GetAircraftTelemetryValidator : AbstractValidator<GetAircraftTelemetry>
{
    public GetAircraftTelemetryValidator()
    {
        RuleFor(x => x.AircraftId)
            .NotNull()
            .WithMessage("AircraftId is required");

        RuleFor(x => x.AircraftId.Value)
            .NotEqual(Guid.Empty)
            .WithMessage("AircraftId cannot be empty");
    }
}

internal class GetAircraftTelemetryHandler : IRequestHandler<GetAircraftTelemetry, GetAircraftTelemetryResult>
{
    private readonly FlightDbContext _flightDbContext;
    private readonly FlightReadDbContext _flightReadDbContext;
    private readonly IMapper _mapper;
    private readonly ILogger<GetAircraftTelemetryHandler> _logger; // Added logger

    public GetAircraftTelemetryHandler(
        FlightDbContext flightDbContext,
        FlightReadDbContext flightReadDbContext,
        IMapper mapper,
        ILogger<GetAircraftTelemetryHandler> logger) // Added logger parameter
    {
        _flightDbContext = flightDbContext;
        _flightReadDbContext = flightReadDbContext;
        _mapper = mapper;
        _logger = logger; // Assigned logger
    }

    public async Task<GetAircraftTelemetryResult> Handle(GetAircraftTelemetry request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        var handlerStartTime = DateTime.UtcNow;
        _logger.LogInformation("🔍 Handler called at: {Timestamp} for AircraftId: {AircraftId}",
            handlerStartTime, request.AircraftId.Value);

        // Find aircraft in PostgreSQL database for static data
        var aircraft = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _flightDbContext.Aircraft.AsNoTracking(), // Already disabled EF Core caching
            x => x.Id == request.AircraftId && !x.IsDeleted,
            cancellationToken);

        if (aircraft == null)
        {
            throw new AircraftNotFoundException(request.AircraftId.Value);
        }

        _logger.LogInformation("📊 PostgreSQL Aircraft found: {Name} ({Model})",
            aircraft.Name.Value, aircraft.Model.Value);

        // Get telemetry data from MongoDB read model (projection updated by ROS2 events)
        var mongoFilter = Builders<AircraftReadModel>.Filter.And(
            Builders<AircraftReadModel>.Filter.Eq(x => x.AircraftId, request.AircraftId.Value),
            Builders<AircraftReadModel>.Filter.Eq(x => x.IsDeleted, false)
        );

        // Log the MongoDB query for debugging
        _logger.LogInformation("🔍 MongoDB Query Filter: AircraftId={AircraftId}, IsDeleted=false",
            request.AircraftId.Value);

        var aircraftReadModel = await _flightReadDbContext.Aircraft
            .Find(mongoFilter)
            .FirstOrDefaultAsync(cancellationToken);

        // Log MongoDB query results
        if (aircraftReadModel != null)
        {
            _logger.LogInformation("📊 MongoDB AircraftReadModel found:");
            _logger.LogInformation("  - LastTelemetryUpdate: {LastUpdate}", aircraftReadModel.LastTelemetryUpdate);
            _logger.LogInformation("  - Position: Lat={Lat}, Lon={Lon}, Alt={Alt}",
                aircraftReadModel.Latitude, aircraftReadModel.Longitude, aircraftReadModel.Altitude);
            _logger.LogInformation("  - Attitude: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}",
                aircraftReadModel.Roll, aircraftReadModel.Pitch, aircraftReadModel.Yaw);
            _logger.LogInformation("  - Telemetry: Speed={Speed}, Heading={Heading}, Fuel={Fuel}, Phase={Phase}",
                aircraftReadModel.Speed, aircraftReadModel.Heading, aircraftReadModel.FuelLevel, aircraftReadModel.FlightPhase);
        }
        else
        {
            _logger.LogWarning("⚠️ No MongoDB AircraftReadModel found for AircraftId: {AircraftId}", request.AircraftId.Value);
        }

        // Check if we have telemetry data
        bool hasTelemetry = aircraftReadModel?.LastTelemetryUpdate != null;
        var currentTime = DateTime.UtcNow;
        var dataAge = hasTelemetry ? currentTime - aircraftReadModel!.LastTelemetryUpdate!.Value : TimeSpan.Zero;

        _logger.LogInformation("⏰ Telemetry Data Age: {Age} (Current: {Current}, LastUpdate: {LastUpdate})",
            dataAge, currentTime, aircraftReadModel?.LastTelemetryUpdate);

        // Build response with both static data (PostgreSQL) and live telemetry (MongoDB)
        var response = new AircraftTelemetryResponse
        {
            AircraftId = aircraft.Id.Value,
            Name = aircraft.Name.Value,
            Model = aircraft.Model.Value,
            ManufacturingYear = aircraft.ManufacturingYear.Value,

            // Position data from ROS2 GPS topic
            Position = hasTelemetry && HasValidPosition(aircraftReadModel)
                ? new PositionDto
                {
                    Latitude = aircraftReadModel!.Latitude!.Value,
                    Longitude = aircraftReadModel.Longitude!.Value,
                    Altitude = aircraftReadModel.Altitude!.Value
                }
                : null,

            // Attitude data from ROS2 attitude topic  
            Attitude = hasTelemetry && HasValidAttitude(aircraftReadModel)
                ? new AttitudeDto
                {
                    Roll = aircraftReadModel!.Roll!.Value,
                    Pitch = aircraftReadModel.Pitch!.Value,
                    Yaw = aircraftReadModel.Yaw!.Value
                }
                : null,

            // Flight telemetry from ROS2 topics (speed, heading, fuel, phase)
            Telemetry = hasTelemetry && HasValidTelemetry(aircraftReadModel)
                ? new TelemetryDto
                {
                    Speed = aircraftReadModel!.Speed!.Value,
                    Heading = aircraftReadModel.Heading!.Value,
                    FuelLevel = aircraftReadModel.FuelLevel!.Value,
                    FlightPhase = aircraftReadModel.FlightPhase ?? "UNKNOWN"
                }
                : null,

            LastUpdate = aircraftReadModel?.LastTelemetryUpdate,
            HasTelemetry = hasTelemetry
        };

        var handlerEndTime = DateTime.UtcNow;
        var handlerDuration = handlerEndTime - handlerStartTime;

        _logger.LogInformation("✅ Handler completed at: {Timestamp} (Duration: {Duration}ms)",
            handlerEndTime, handlerDuration.TotalMilliseconds);
        _logger.LogInformation("📤 Response: HasTelemetry={HasTelemetry}, LastUpdate={LastUpdate}, DataAge={DataAge}",
            response.HasTelemetry, response.LastUpdate, dataAge);

        return new GetAircraftTelemetryResult(response);
    }

    private static bool HasValidPosition(AircraftReadModel? model) =>
        model?.Latitude.HasValue == true &&
        model.Longitude.HasValue == true &&
        model.Altitude.HasValue == true;

    private static bool HasValidAttitude(AircraftReadModel? model) =>
        model?.Roll.HasValue == true &&
        model.Pitch.HasValue == true &&
        model.Yaw.HasValue == true;

    private static bool HasValidTelemetry(AircraftReadModel? model) =>
        model?.Speed.HasValue == true &&
        model.Heading.HasValue == true &&
        model.FuelLevel.HasValue == true;
}