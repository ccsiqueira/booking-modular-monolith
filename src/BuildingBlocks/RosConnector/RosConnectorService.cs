using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BuildingBlocks.RosConnector;

// Note: This is a simplified ROS connector for demo purposes
// In a real implementation, you would use proper ROS# libraries
public class RosConnectorService : IRosConnectorService, IDisposable
{
    private readonly ILogger<RosConnectorService> _logger;
    private readonly ConcurrentDictionary<string, object> _latestMessages = new();
    private readonly ConcurrentDictionary<string, bool> _subscribedTopics = new();
    private ClientWebSocket? _webSocket;
    private string? _rosUri;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isConnected;

    // Topic to ROS message type mapping
    private readonly Dictionary<string, string> _topicTypes = new()
    {
        ["gps"] = "sensor_msgs/NavSatFix",
        ["attitude"] = "geometry_msgs/Vector3",
        ["velocity"] = "geometry_msgs/Vector3", 
        ["airspeed"] = "std_msgs/Float64",
        ["altitude"] = "std_msgs/Float64",
        ["heading"] = "std_msgs/Float64",
        ["flight_phase"] = "std_msgs/String",
        ["battery"] = "std_msgs/Float64"
    };

    public RosConnectorService(ILogger<RosConnectorService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(string rosUri)
    {
        try
        {
            _rosUri = rosUri;
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();

            await _webSocket.ConnectAsync(new Uri(rosUri), _cancellationTokenSource.Token);
            _isConnected = true;

            _logger.LogInformation("Connected to ROS2 at {Uri}", rosUri);

            // Start listening for messages in background
            _ = Task.Run(ListenForMessages, _cancellationTokenSource.Token);

            return true;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to ROS2 at {Uri}", rosUri);
            _isConnected = false;
            return false;
        }
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected && _webSocket?.State == WebSocketState.Open);
    }

    public async Task<T?> GetLatestAsync<T>(string topicName, int timeoutMs = 1000) where T : class
    {
        if (!_isConnected || _webSocket?.State != WebSocketState.Open)
        {
            _logger.LogWarning("Not connected to ROS2. Cannot get data from topic {TopicName}", topicName);
            return null;
        }

        try
        {
            // Subscribe to topic if not already subscribed
            if (!_subscribedTopics.ContainsKey(topicName))
            {
                await SubscribeToTopicAsync(topicName);
            }

            // Wait for data or timeout
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_latestMessages.TryGetValue(topicName, out var message))
                {
                    if (message is T typedMessage)
                    {
                        return typedMessage;
                    }
                    
                    // Try to convert from dynamic object to T
                    var json = JsonSerializer.Serialize(message);
                    return JsonSerializer.Deserialize<T>(json);
                }

                await Task.Delay(50); // Check every 50ms
            }

            _logger.LogWarning("Timeout waiting for data from topic {TopicName}", topicName);
            return null;
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error getting data from topic {TopicName}", topicName);
            return null;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            
            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
            }

            _isConnected = false;
            _subscribedTopics.Clear();
            _latestMessages.Clear();
            _logger.LogInformation("Disconnected from ROS2");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from ROS2");
        }
    }

    public async Task PublishToTopicAsync<T>(string topicName, T message) where T : class
    {
        if (!_isConnected || _webSocket?.State != WebSocketState.Open)
        {
            _logger.LogWarning("Not connected to ROS2. Cannot publish to topic {TopicName}", topicName);
            return;
        }

        try
        {
            var messageType = GetMessageTypeForTopic(topicName);
            
            var publishMessage = new
            {
                op = "publish",
                topic = topicName,
                type = messageType,
                msg = message
            };

            var json = JsonSerializer.Serialize(publishMessage);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None);

            _logger.LogDebug("Published message to topic {TopicName}", topicName);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error publishing to topic {TopicName}", topicName);
        }
    }

    private async Task SubscribeToTopicAsync(string topicName)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        try
        {
            // Determine the correct message type based on topic name
            var messageType = GetMessageTypeForTopic(topicName);
            
            var subscribeMessage = new
            {
                op = "subscribe",
                topic = topicName,
                type = messageType
            };

            var json = JsonSerializer.Serialize(subscribeMessage);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes), 
                WebSocketMessageType.Text, 
                true, 
                CancellationToken.None);

            // Mark as subscribed
            _subscribedTopics.TryAdd(topicName, true);
            _logger.LogDebug("Subscribed to topic {TopicName} with type {MessageType}", topicName, messageType);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to topic {TopicName}", topicName);
        }
    }

    private string GetMessageTypeForTopic(string topicName)
    {
        // Extract the topic type from the full topic path
        // e.g., "/aircraft/AIRCRAFT_001/gps" -> "gps"
        var parts = topicName.Split('/');
        var topicType = parts.LastOrDefault() ?? "";
        
        return _topicTypes.TryGetValue(topicType, out var messageType) 
            ? messageType 
            : "std_msgs/String"; // Default fallback
    }

    private async Task ListenForMessages()
    {
        if (_webSocket == null || _cancellationTokenSource == null) return;

        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    _cancellationTokenSource.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    ProcessMessage(message);
                }
            }
        }
        catch (System.Exception ex) when (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _logger.LogError(ex, "Error listening for ROS2 messages");
            _isConnected = false;
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            _logger.LogDebug("Raw ROS message received: {Message}", message);
            
            var jsonDoc = JsonDocument.Parse(message);
            
            if (jsonDoc.RootElement.TryGetProperty("topic", out var topicProperty) &&
                jsonDoc.RootElement.TryGetProperty("msg", out var msgProperty))
            {
                var topicName = topicProperty.GetString();
                if (!string.IsNullOrEmpty(topicName))
                {
                    // Store the message data
                    var messageData = JsonSerializer.Deserialize<object>(msgProperty.GetRawText());
                    if (messageData != null)
                    {
                        _latestMessages.AddOrUpdate(topicName, messageData, (key, oldValue) => messageData);
                        _logger.LogInformation("✅ Received message from topic {TopicName}: {MessageData}", topicName, msgProperty.GetRawText());
                    }
                }
            }
            else if (jsonDoc.RootElement.TryGetProperty("op", out var opProperty))
            {
                // Handle rosbridge status messages
                var op = opProperty.GetString();
                _logger.LogDebug("Received rosbridge operation: {Operation}, Message: {Message}", op, message);
            }
            else
            {
                _logger.LogWarning("Received unrecognized message format: {Message}", message);
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing ROS2 message: {Message}", message);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

