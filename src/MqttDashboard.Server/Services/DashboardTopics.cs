namespace MqttDashboard.Server.Services;

/// <summary>
/// Constants for virtual $DASHBOARD/* topics published by <see cref="DashboardMetricsPublisher"/>
/// into <see cref="ServerDataCache"/>. Any widget can subscribe to these topics to display
/// live diagnostic data without ad-hoc service calls.
/// </summary>
public static class DashboardTopics
{
    public const string Root          = "$DASHBOARD";
    public const string Time          = "$DASHBOARD/TIME";
    public const string Uptime        = "$DASHBOARD/UPTIME";
    public const string Version       = "$DASHBOARD/VERSION";
    public const string VersionLatest = "$DASHBOARD/VERSION/LATEST";
    public const string MqttStatus    = "$DASHBOARD/MQTT/STATUS";
    public const string MqttBroker    = "$DASHBOARD/MQTT/BROKER";
    public const string MqttTopicCount = "$DASHBOARD/MQTT/TOPIC_COUNT";
    public const string ClientsCount  = "$DASHBOARD/CLIENTS/COUNT";
}
