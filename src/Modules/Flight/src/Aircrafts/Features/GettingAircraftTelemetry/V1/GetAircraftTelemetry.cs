namespace Flight.Aircrafts.Features.GettingAircraftTelemetry.V1;

using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.EventStoreDB.Repository;
using BuildingBlocks.Web;
using Duende.IdentityServer.EntityFramework.Entities;
using Flight.Aircrafts.Exceptions;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
            .RequireAuthorization(nameof(ApiScope))
            .WithName("GetAircraftTelemetry")
            .WithApiVersionSet(builder.NewApiVersionSet("Flight").Build())
            .Produces<AircraftTelemetryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Get current aircraft telemetry frame")
            .WithDescription("Returns the current real-time telemetry data for a specific aircraft including position, attitude, speed, and other flight data. Requires authentication.")
            .WithOpenApi()
            .HasApiVersion(1.0);

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
    private readonly IEventStoreDBRepository<Aircraft> _aircraftRepository;
    private readonly IMapper _mapper;

    public GetAircraftTelemetryHandler(
        IEventStoreDBRepository<Aircraft> aircraftRepository,
        IMapper mapper)
    {
        _aircraftRepository = aircraftRepository;
        _mapper = mapper;
    }

    public async Task<GetAircraftTelemetryResult> Handle(GetAircraftTelemetry request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request, nameof(request));

        // Find aircraft in event store (reconstructed from events)
        var aircraft = await _aircraftRepository.FindAsync(request.AircraftId, cancellationToken);

        if (aircraft == null)
        {
            throw new AircraftNotFoundException(request.AircraftId.Value);
        }

        // Map aircraft data to response DTO
        var response = new AircraftTelemetryResponse
        {
            AircraftId = aircraft.Id.Value,
            Name = aircraft.Name.Value,
            Model = aircraft.Model.Value,
            ManufacturingYear = aircraft.ManufacturingYear.Value,
            Position = aircraft.CurrentPosition != null ? new PositionDto
            {
                Latitude = aircraft.CurrentPosition.Latitude,
                Longitude = aircraft.CurrentPosition.Longitude,
                Altitude = aircraft.CurrentPosition.Altitude
            } : null,
            Attitude = aircraft.CurrentAttitude != null ? new AttitudeDto
            {
                Roll = aircraft.CurrentAttitude.Roll,
                Pitch = aircraft.CurrentAttitude.Pitch,
                Yaw = aircraft.CurrentAttitude.Yaw
            } : null,
            Telemetry = aircraft.CurrentTelemetry != null ? new TelemetryDto
            {
                Speed = aircraft.CurrentTelemetry.Speed,
                Heading = aircraft.CurrentTelemetry.Heading,
                FuelLevel = aircraft.CurrentTelemetry.FuelLevel,
                FlightPhase = aircraft.CurrentTelemetry.FlightPhase
            } : null,
            LastUpdate = aircraft.LastTelemetryUpdate,
            HasTelemetry = aircraft.CurrentPosition != null && aircraft.CurrentAttitude != null && aircraft.CurrentTelemetry != null
        };

        return new GetAircraftTelemetryResult(response);
    }
}