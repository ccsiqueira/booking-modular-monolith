using BuildingBlocks.EFCore;
using BuildingBlocks.EventStoreDB;
using BuildingBlocks.Mapster;
using BuildingBlocks.Mongo;
using BuildingBlocks.RosConnector;
using BuildingBlocks.Web;
using Flight.Aircrafts.Services;
using Flight.Data;
using Flight.Data.Seed;
using Flight.GrpcServer.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Flight.Extensions.Infrastructure;


public static class InfrastructureExtensions
{
    public static WebApplicationBuilder AddFlightModules(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<FlightEventMapper>();
        builder.AddMinimalEndpoints(assemblies: typeof(FlightRoot).Assembly);
        builder.Services.AddValidatorsFromAssembly(typeof(FlightRoot).Assembly);
        builder.Services.AddCustomMapster(typeof(FlightRoot).Assembly);
        builder.AddCustomDbContext<FlightDbContext>(nameof(Flight));
        builder.Services.AddScoped<IDataSeeder, FlightDataSeeder>();
        builder.AddMongoDbContext<FlightReadDbContext>();

        // Add EventStore support for Aircraft telemetry events
        builder.Services.AddEventStore(builder.Configuration, typeof(FlightRoot).Assembly)
            .AddEventStoreDBSubscriptionToAll();

        // Add ROS2 connector for aircraft telemetry
        builder.Services.AddRosConnector(builder.Configuration);
        
        // Configure telemetry collector options
        builder.Services.Configure<AircraftTelemetryCollectorOptions>(
            builder.Configuration.GetSection(AircraftTelemetryCollectorOptions.SectionName));
        
        // Add always-on background service for telemetry collection
        builder.Services.AddHostedService<AircraftTelemetryCollectorService>();

        builder.Services.AddCustomMediatR();

        return builder;
    }


    public static WebApplication UseFlightModules(this WebApplication app)
    {
        app.UseMigration<FlightDbContext>();
        app.MapGrpcService<FlightGrpcServices>();

        return app;
    }
}