using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.Protocols;
using RosString = RosSharp.RosBridgeClient.MessageTypes.Std.String;

namespace BuildingBlocks.RosConnector;

/// <summary>
/// ROS# (ROS Sharp) based connector for ROS2 integration via rosbridge protocol
/// Replaces the custom WebSocket implementation with official ROS# libraries
/// </summary>
public class RosConnectorService : IRosConnectorService, IDisposable
{
    private readonly ILogger<RosConnectorService> _logger;
    private readonly ConcurrentDictionary<string, object> _latestMessages = new();
    private readonly ConcurrentDictionary<string, string> _subscriptions = new();
    private RosSocket? _rosSocket;
    private string? _rosUri;
    private bool _isConnected;

    public RosConnectorService(ILogger<RosConnectorService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string rosUri)
    {
        try
        {
            _rosUri = rosUri;

            // Create the WebSocket protocol using ROS# WebSocketNetProtocol
            var protocol = new WebSocketNetProtocol(rosUri);

            // Create RosSocket with Microsoft serializer (default, more performant)
            _rosSocket = new RosSocket(protocol, RosSocket.SerializerEnum.Microsoft);

            _isConnected = true;
            _logger.LogInformation("🔗 Connected to ROS2 via ROS# at {Uri}", rosUri);

            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to connect to ROS2 at {Uri}", rosUri);
            _isConnected = false;
            return false;
        }
        finally
        {
            await Task.CompletedTask; // Maintain async signature
        }
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected && _rosSocket != null);
    }

    public async Task<T?> GetLatestAsync<T>(string topicName, int timeoutMs = 1000) where T : class
    {
        if (!_isConnected || _rosSocket == null)
        {
            _logger.LogWarning("⚠️ Not connected to ROS2. Cannot get data from topic {TopicName}", topicName);
            return null;
        }

        try
        {
            // Subscribe to topic if not already subscribed
            SubscribeToTopicIfNeeded(topicName);

            // Wait for data or timeout
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_latestMessages.TryGetValue(topicName, out var message))
                {
                    // If caller asked for the ROS# message type, return directly
                    if (message is T typedMessage)
                    {
                        return typedMessage;
                    }

                    // Otherwise try to map via JSON for caller-defined DTOs (backward compatibility)
                    try
                    {
                        var json = JsonSerializer.Serialize(message);
                        var mapped = JsonSerializer.Deserialize<T>(json);
                        if (mapped != null) return mapped;
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogDebug(ex, "JSON mapping failed for topic {Topic} to type {Type}", topicName, typeof(T).FullName);
                    }
                }

                await Task.Delay(50); // Check every 50ms
            }

            _logger.LogWarning("⏰ Timeout waiting for data from topic {TopicName}", topicName);
            return null;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting data from topic {TopicName}", topicName);
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_rosSocket != null)
            {
                // Unsubscribe from all topics
                foreach (var subscriptionId in _subscriptions.Values)
                {
                    try
                    {
                        _rosSocket.Unsubscribe(subscriptionId);
                    }
                    catch
                    {
                        /* best effort unsubscribe */
                    }
                }
                _subscriptions.Clear();
                _latestMessages.Clear();

                // Close the socket with a small wait time for cleanup
                _rosSocket.Close(100);
                _rosSocket = null;
            }

            _isConnected = false;
            _logger.LogInformation("🔌 Disconnected from ROS2 via ROS#");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "❌ Error disconnecting from ROS2");
        }
        finally
        {
            await Task.CompletedTask; // Maintain async signature
        }
    }

    private void SubscribeToTopicIfNeeded(string topicName)
    {
        if (_rosSocket == null) return;
        if (_subscriptions.ContainsKey(topicName)) return;

        string? subscriptionId = null;

        // Map topics to strongly-typed ROS# subscriptions using our message types
        // Each subscription writes the latest message into _latestMessages[topic]
        try
        {
            switch (topicName)
            {
                case "/aircraft/AIRCRAFT_001/gps":
                    subscriptionId = _rosSocket.Subscribe<NavSatFix>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received GPS data from {Topic}", topicName);
                    });
                    break;

                case "/aircraft/AIRCRAFT_001/velocity":
                    subscriptionId = _rosSocket.Subscribe<Twist>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received velocity data from {Topic}", topicName);
                    });
                    break;

                case "/aircraft/AIRCRAFT_001/attitude":
                    subscriptionId = _rosSocket.Subscribe<Vector3Stamped>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received attitude data from {Topic}", topicName);
                    });
                    break;

                case "/aircraft/AIRCRAFT_001/altitude":
                case "/aircraft/AIRCRAFT_001/airspeed":
                case "/aircraft/AIRCRAFT_001/heading":
                    subscriptionId = _rosSocket.Subscribe<Float64>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received Float64 data from {Topic}: {Value}", topicName, msg.data);
                    });
                    break;

                case "/aircraft/AIRCRAFT_001/battery":
                    subscriptionId = _rosSocket.Subscribe<BatteryState>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received battery data from {Topic}: {Percentage}%", topicName, msg.percentage * 100);
                    });
                    break;

                case "/aircraft/AIRCRAFT_001/flight_phase":
                    subscriptionId = _rosSocket.Subscribe<RosString>(topicName, msg =>
                    {
                        _latestMessages[topicName] = msg;
                        _logger.LogDebug("📨 Received flight phase from {Topic}: {Phase}", topicName, msg.data);
                    });
                    break;

                default:
                    _logger.LogWarning("⚠️ No known type mapping for topic {Topic}", topicName);
                    return;
            }

            if (!string.IsNullOrEmpty(subscriptionId))
            {
                _subscriptions[topicName] = subscriptionId;
                _logger.LogInformation("📡 Subscribed to {Topic} via ROS# (ID: {Id})", topicName, subscriptionId);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to subscribe to topic {Topic}", topicName);
        }
    }

    public void Dispose()
    {
        try
        {
            DisconnectAsync().GetAwaiter().GetResult();
        }
        catch
        {
            /* ignore dispose errors */
        }
    }
}