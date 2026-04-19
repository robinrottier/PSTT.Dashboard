using System.Net;

namespace MqttDashboard.Helpers;

/// <summary>
/// XML payload utilities for sanitising MQTT strings before embedding in XML/HTML contexts.
/// </summary>
public static class XmlStringHelper
{
    /// <summary>Strips characters that are illegal in XML 1.0 (C0/C1 controls and lone surrogates).</summary>
    public static string StripInvalidXmlChars(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c == 0x9 || c == 0xA || c == 0xD) { sb.Append(c); continue; }
            if (c < 0x20) continue;                   // C0 controls
            if (c >= 0x7F && c <= 0x9F) continue;     // C1 controls (includes DEL)
            if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c)) continue; // lone surrogates
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Strips illegal XML characters then HTML-encodes the result.</summary>
    public static string XmlSafeEncode(string? s) => WebUtility.HtmlEncode(StripInvalidXmlChars(s));
}
