using MqttDashboard.Data;

namespace MqttDashboard.Helpers;

/// <summary>
/// Retained for backward compatibility — delegates to <see cref="XmlPayloadHelper"/>
/// which now lives in <c>MqttDashboard.Data</c>.
/// </summary>
public static class XmlStringHelper
{
    public static string StripInvalidXmlChars(string? s) => XmlPayloadHelper.StripInvalidXmlChars(s);
    public static string XmlSafeEncode(string? s) => XmlPayloadHelper.XmlSafeEncode(s);
}
