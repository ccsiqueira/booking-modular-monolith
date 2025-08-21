using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Core;
using BuildingBlocks.Core.Event;
using BuildingBlocks.RosConnector;
using Flight.Aircrafts.Events;
using Flight.Aircrafts.Features.GettingAircraftTelemetry.V1;
using Flight.Aircrafts.Models;
using Flight.Aircrafts.ValueObjects;
using Flight.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosString = RosSharp.RosBridgeClient.MessageTypes.Std.String;

namespace Flight.Aircrafts.Services;

public class AircraftTelemetryCollectorService : BackgroundService
{
    private readonly IRosConnectorService _rosConnector;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AircraftTelemetryCollectorService> _logger;
    private readonly AircraftTelemetryCollectorOptions _options;

    public AircraftTelemetryCollectorService(
        IRosConnectorService rosConnector,
        IServiceProvider serviceProvider,
        ILogger<AircraftTelemetryCollectorService> logger,
        IOptions<AircraftTelemetryCollectorOptions> options)
    {
        _rosConnector = rosConnector;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Aircraft Telemetry Collector Service started - Always ON mode");

        // Connect to ROS2
        var connected = await _rosConnector.ConnectAsync(_options.RosUri);
        if (!connected)
        {
            _logger.LogError("Failed to connect to ROS2 at {Uri}. Service will retry connection.", _options.RosUri);
        }

        // Always running - no start/stop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Ensure we're still connected
                if (!await _rosConnector.IsConnectedAsync())
                {
                    _logger.LogWarning("Lost connection to ROS2. Attempting to reconnect...");
                    await _rosConnector.ConnectAsync(_options.RosUri);
                }

                await CollectAndUpdateTelemetryAsync(stoppingToken);

                // Wait for next collection cycle (1Hz frequency)
                await Task.Delay(TimeSpan.FromSeconds(_options.CollectionIntervalSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in telemetry collection cycle");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Error delay
            }
        }

        _logger.LogInformation("Aircraft Telemetry Collector Service stopped");
    }

    private async Task CollectAndUpdateTelemetryAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            var collectionStartTime = DateTime.UtcNow;
            _logger.LogInformation("🔄 Starting telemetry collection at: {Timestamp}", collectionStartTime);
            
            // Collect telemetry data from ROS2 topics
            var telemetryData = await CollectTelemetryFromRosAsync();

            if (telemetryData == null)
            {
                _logger.LogDebug("No telemetry data received from ROS2");
                return;
            }

            _logger.LogInformation("📡 ROS2 telemetry collected successfully at: {Timestamp}", DateTime.UtcNow);
            _logger.LogInformation("  - Position: {Position}", telemetryData.Position);
            _logger.LogInformation("  - Attitude: {Attitude}", telemetryData.Attitude);
            _logger.LogInformation("  - Telemetry: {Telemetry}", telemetryData.TelemetryData);

            // Convert string aircraft ID to Guid
            var aircraftGuid = ConvertStringToGuid(_options.AircraftId);
            _logger.LogInformation("🆔 Using Aircraft GUID: {AircraftGuid} for string ID: {StringId}", 
                aircraftGuid, _options.AircraftId);

            // Option 2: Fallback to direct MongoDB update
            await UpdateMongoDbDirectly(scope, aircraftGuid, telemetryData, cancellationToken);

            /*
            // Option 1: Try to use EventDispatcher for internal command (preferred)
            try
            {
                var eventDispatcher = scope.ServiceProvider.GetRequiredService<IEventDispatcher>();
                
                // Create and publish telemetry event
                var telemetryEvent = new AircraftTelemetryUpdatedDomainEvent(
                    aircraftGuid,
                    telemetryData.Position ?? Position.Empty,
                    telemetryData.Attitude ?? Attitude.Empty,
                    telemetryData.TelemetryData ?? TelemetryData.Empty,
                    DateTime.UtcNow);

                _logger.LogInformation("📤 Dispatching AircraftTelemetryUpdatedDomainEvent via EventDispatcher");
                _logger.LogInformation("  - Event Type: {EventType}", telemetryEvent.GetType().Name);
                _logger.LogInformation("  - Timestamp: {Timestamp}", telemetryEvent.Timestamp);
                _logger.LogInformation("  - Aircraft ID: {AircraftId}", telemetryEvent.AircraftId);

                // Dispatch the event as internal command to update MongoDB
                await eventDispatcher.SendAsync(telemetryEvent, typeof(IInternalCommand), cancellationToken: cancellationToken);

                var dispatchEndTime = DateTime.UtcNow;
                var dispatchDuration = dispatchEndTime - collectionStartTime;
                
                _logger.LogInformation("✅ Telemetry event dispatched successfully at: {Timestamp} (Total duration: {Duration}ms)", 
                    dispatchEndTime, dispatchDuration.TotalMilliseconds);
                _logger.LogInformation("📊 Event details: {Position}, {Attitude}, {TelemetryData}",
                    telemetryData.Position, telemetryData.Attitude, telemetryData.TelemetryData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EventDispatcher failed, falling back to direct MongoDB update");
                
                // Option 2: Fallback to direct MongoDB update
                await UpdateMongoDbDirectly(scope, aircraftGuid, telemetryData, cancellationToken);
            }*/
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing aircraft telemetry event");
        }
    }

    private async Task UpdateMongoDbDirectly(IServiceScope scope, Guid aircraftId, CollectedTelemetryData telemetryData, CancellationToken cancellationToken)
    {
        try
        {
            var flightReadDbContext = scope.ServiceProvider.GetRequiredService<FlightReadDbContext>();
            
            var filter = Builders<AircraftReadModel>.Filter.And(
                Builders<AircraftReadModel>.Filter.Eq(x => x.AircraftId, aircraftId),
                Builders<AircraftReadModel>.Filter.Eq(x => x.IsDeleted, false)
            );

            var update = Builders<AircraftReadModel>.Update
                .Set(x => x.Latitude, telemetryData.Position?.Latitude)
                .Set(x => x.Longitude, telemetryData.Position?.Longitude)
                .Set(x => x.Altitude, telemetryData.Position?.Altitude)
                .Set(x => x.Roll, telemetryData.Attitude?.Roll)
                .Set(x => x.Pitch, telemetryData.Attitude?.Pitch)
                .Set(x => x.Yaw, telemetryData.Attitude?.Yaw)
                .Set(x => x.Speed, telemetryData.TelemetryData?.Speed)
                .Set(x => x.Heading, telemetryData.TelemetryData?.Heading)
                .Set(x => x.FuelLevel, telemetryData.TelemetryData?.FuelLevel)
                .Set(x => x.FlightPhase, telemetryData.TelemetryData?.FlightPhase)
                .Set(x => x.LastTelemetryUpdate, DateTime.UtcNow);

            var result = await flightReadDbContext.Aircraft.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

            if (result.MatchedCount == 0)
            {
                _logger.LogWarning("No aircraft found in MongoDB for ID {AircraftId}", aircraftId);
            }
            else
            {
                _logger.LogDebug("Directly updated MongoDB for aircraft {AircraftId}: {Position}, {Attitude}, {TelemetryData}",
                    aircraftId, telemetryData.Position, telemetryData.Attitude, telemetryData.TelemetryData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating MongoDB directly for aircraft {AircraftId}", aircraftId);
        }
    }

    private async Task<CollectedTelemetryData?> CollectTelemetryFromRosAsync()
    {
        try
        {
            // Collect data from all ROS2 topics using strongly-typed ROS# message classes
            var gpsTask = _rosConnector.GetLatestAsync<NavSatFix>("/aircraft/AIRCRAFT_001/gps");
            var attitudeTask = _rosConnector.GetLatestAsync<Vector3Stamped>("/aircraft/AIRCRAFT_001/attitude");
            var velocityTask = _rosConnector.GetLatestAsync<Twist>("/aircraft/AIRCRAFT_001/velocity");
            var batteryTask = _rosConnector.GetLatestAsync<BatteryState>("/aircraft/AIRCRAFT_001/battery");
            var altitudeTask = _rosConnector.GetLatestAsync<Float64>("/aircraft/AIRCRAFT_001/altitude");
            var airspeedTask = _rosConnector.GetLatestAsync<Float64>("/aircraft/AIRCRAFT_001/airspeed");
            var headingTask = _rosConnector.GetLatestAsync<Float64>("/aircraft/AIRCRAFT_001/heading");
            var phaseTask = _rosConnector.GetLatestAsync<RosString>("/aircraft/AIRCRAFT_001/flight_phase");

            // Wait for all data collection tasks
            await Task.WhenAll(gpsTask, attitudeTask, velocityTask, batteryTask, altitudeTask, airspeedTask, headingTask, phaseTask);

            var gpsData = await gpsTask;
            var attitudeData = await attitudeTask;
            var velocityData = await velocityTask;
            var batteryData = await batteryTask;
            var altitudeData = await altitudeTask;
            var airspeedData = await airspeedTask;
            var headingData = await headingTask;
            var phaseData = await phaseTask;

            // Debug each topic data with strongly-typed messages
            _logger.LogInformation("📊 ROS# Topic data status:");
            _logger.LogInformation("  GPS: {Status} - Lat: {Lat}, Lon: {Lon}, Alt: {Alt}",
                GetDataStatus(gpsData), gpsData?.latitude, gpsData?.longitude, gpsData?.altitude);
            _logger.LogInformation("  Attitude: {Status} - Roll: {Roll}, Pitch: {Pitch}, Yaw: {Yaw}",
                GetDataStatus(attitudeData), attitudeData?.vector.x, attitudeData?.vector.y, attitudeData?.vector.z);
            _logger.LogInformation("  Velocity: {Status} - Linear: ({X}, {Y}, {Z})",
                GetDataStatus(velocityData), velocityData?.linear.x, velocityData?.linear.y, velocityData?.linear.z);
            _logger.LogInformation("  Battery: {Status} - Percentage: {Percentage}%",
                GetDataStatus(batteryData), batteryData?.percentage * 100);
            _logger.LogInformation("  Altitude: {Status} - Value: {Value}",
                GetDataStatus(altitudeData), altitudeData?.data);
            _logger.LogInformation("  Airspeed: {Status} - Value: {Value}",
                GetDataStatus(airspeedData), airspeedData?.data);
            _logger.LogInformation("  Heading: {Status} - Value: {Value}",
                GetDataStatus(headingData), headingData?.data);
            _logger.LogInformation("  Phase: {Status} - Value: {Value}",
                GetDataStatus(phaseData), phaseData?.data);

            // Parse and validate the collected data - now using strongly-typed messages
            if (gpsData is null)
            {
                _logger.LogDebug("Missing essential telemetry data (GPS required)");
                return null;
            }

            // Convert strongly-typed ROS# messages to our value objects
            var position = ParsePosition(gpsData, altitudeData);
            var attitude = ParseAttitude(attitudeData);
            var telemetry = ParseTelemetryData(airspeedData, headingData, batteryData, phaseData);

            return new CollectedTelemetryData(position, attitude, telemetry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting telemetry from ROS2");
            return null;
        }
    }

    private Position ParsePosition(NavSatFix? gpsData, Float64? altitudeData)
    {
        try
        {
            if (gpsData == null)
            {
                _logger.LogDebug("GPS data is null, returning empty position");
                return Position.Empty;
            }

            _logger.LogDebug("Parsing GPS data from ROS# NavSatFix message");

            // Extract GPS coordinates directly from strongly-typed message
            var latitude = gpsData.latitude;
            var longitude = gpsData.longitude;
            var altitude = gpsData.altitude;

            _logger.LogDebug("Extracted GPS values: Lat={Lat}, Lon={Lon}, Alt={Alt}", latitude, longitude, altitude);

            var finalLatitude = latitude;
            var finalLongitude = longitude;
            var finalAltitude = altitude;

            // Use separate altitude if available (and convert feet to meters if needed)
            if (altitudeData != null)
            {
                var separateAltitude = altitudeData.data;
                finalAltitude = separateAltitude * 0.3048; // Convert feet to meters
                _logger.LogDebug("Using separate altitude: {Original} ft -> {Converted} m", separateAltitude, finalAltitude);
            }

            var result = Position.Of(finalLatitude, finalLongitude, finalAltitude);
            _logger.LogDebug("Created position: {Position}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing position data from ROS# messages");
            return Position.Empty;
        }
    }

    private Attitude ParseAttitude(Vector3Stamped? attitudeData)
    {
        try
        {
            if (attitudeData?.vector == null)
            {
                _logger.LogDebug("Attitude data is null, returning empty attitude");
                return Attitude.Empty;
            }

            _logger.LogDebug("Parsing attitude data from ROS# Vector3Stamped message");

            // Extract attitude values directly from strongly-typed message
            var roll = attitudeData.vector.x;
            var pitch = attitudeData.vector.y;
            var yaw = attitudeData.vector.z;

            _logger.LogDebug("Extracted attitude values: Roll={Roll}, Pitch={Pitch}, Yaw={Yaw}", roll, pitch, yaw);

            return Attitude.Of(roll, pitch, yaw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing attitude data from ROS# message");
            return Attitude.Empty;
        }
    }

    private TelemetryData ParseTelemetryData(Float64? airspeedData, Float64? headingData, BatteryState? batteryData, RosString? phaseData)
    {
        try
        {
            _logger.LogDebug("Parsing telemetry data from ROS# messages");

            // Extract values directly from strongly-typed messages
            var speed = airspeedData?.data ?? 0.0;
            var heading = headingData?.data ?? 0.0;
            var fuelLevel = batteryData?.percentage ?? 0.0;
            var flightPhase = phaseData?.data ?? "UNKNOWN";

            // Convert fuel percentage to 0-100 range if needed
            if (fuelLevel <= 1.0) fuelLevel *= 100.0;

            _logger.LogDebug("Extracted telemetry values: Speed={Speed}, Heading={Heading}, Fuel={Fuel}%, Phase={Phase}",
                speed, heading, fuelLevel, flightPhase);

            return TelemetryData.Of(speed, heading, fuelLevel, flightPhase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing telemetry data from ROS# messages");
            return TelemetryData.Empty;
        }
    }



    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Aircraft Telemetry Collector Service stopping...");
        await _rosConnector.DisconnectAsync();
        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Converts a string aircraft ID to "3c5c0000-97c6-fc34-fcd3-08db322230c8".
    /// This ensures the same string always maps to the same Guid.
    /// </summary>
    private static Guid ConvertStringToGuid(string aircraftId)
    {
        if (string.IsNullOrWhiteSpace(aircraftId))
        {
            return Guid.Empty;
        }

        //using var md5 = MD5.Create();
        //var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(aircraftId));
        return new Guid("3c5c0000-97c6-fc34-fcd3-08db322230c8");
    }

    /// <summary>
    /// Safely determines if ROS# message data is available
    /// </summary>
    private string GetDataStatus<T>(T? data) where T : class
    {
        return data is null ? "NULL" : "RECEIVED";
    }
}

// Helper classes
public record CollectedTelemetryData(Position Position, Attitude Attitude, TelemetryData TelemetryData);

public class AircraftTelemetryCollectorOptions
{
    public const string SectionName = "AircraftTelemetryCollector";

    public string RosUri { get; set; } = "ws://localhost:9090";
    public string AircraftId { get; set; } = "AIRCRAFT_001"; // Default to our simulated aircraft
    public int CollectionIntervalSeconds { get; set; } = 1; // 1Hz frequency
    public int MaxRetries { get; set; } = 3;
    public bool EnableLiveData { get; set; } = true;
}