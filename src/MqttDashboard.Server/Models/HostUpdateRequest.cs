namespace MqttDashboard.Server.Models;

public class HostUpdateRequest
{
    public string? Service { get; set; }
    public string? ComposeFile { get; set; }
    public string? WatchtowerContainer { get; set; }
    public string? Workdir { get; set; }
    // Optional: a secret token entered by the user (prompted) to be sent to the update agent.
    // If provided by the client this value will be used instead of the configured token.
    public string? AgentToken { get; set; }
}
