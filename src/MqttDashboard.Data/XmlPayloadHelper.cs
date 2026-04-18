using System.Net;
using System.Text;

namespace MqttDashboard.Data;

/// <summary>
/// Helpers for producing strings safe to inject into XML/SVG/HTML DOM contexts.
/// </summary>
public static class XmlPayloadHelper
{
    /// <summary>
    /// Strips characters that are illegal in XML 1.0 (null bytes, lone surrogates,
    /// and C0/C1 control characters other than tab, LF and CR).
    /// This prevents InvalidCharacterError when the string is placed into the DOM,
    /// particularly in SVG elements which are parsed as XML.
    /// </summary>
    public static string StripInvalidXmlChars(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? string.Empty;

        StringBuilder? sb = null;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool valid = c == '\t' || c == '\n' || c == '\r'
                || (c >= 0x0020 && c <= 0xD7FF)
                || (c >= 0xE000 && c <= 0xFFFD);

            if (!valid)
                sb ??= new StringBuilder(s, 0, i, s.Length);
            else
                sb?.Append(c);
        }
        return sb?.ToString() ?? s;
    }

    /// <summary>
    /// Strips invalid XML characters AND HTML-encodes the result.
    /// Use this when injecting dynamic values into SVG/XML MarkupString content.
    /// </summary>
    public static string XmlSafeEncode(string? s) =>
        WebUtility.HtmlEncode(StripInvalidXmlChars(s));
}
