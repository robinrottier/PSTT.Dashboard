using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components;
using PSTT.Dashboard.Models;
using System.Text.RegularExpressions;

namespace PSTT.Dashboard.Widgets;

/// <summary>
/// Extends <see cref="BaseNodeWidget{TNode}"/> with automatic MQTT data setup
/// Override <see cref="OnDataUpdated"/> to react to new values.
/// </summary>
public abstract class BaseNodeWithDataWidget<TNode> : BaseNodeWidget<TNode>
    where TNode : TextNodeModel
{
    private readonly List<IDisposable> _dataWatchers = new();
    private bool _disposed = false;
    // Track the last topics key so we skip SetupDataWatchers when nothing has changed.
    private string? _watcherTopicsKey = null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        SetupDataWatchers();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        SetupDataWatchers();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
    }

    protected void SetupDataWatchers()
    {
        // Skip rebuild if topics haven't changed — prevents redundant re-runs
        // from every OnParametersSet (triggered by RefreshAll, StateHasChanged, etc.).
        var topicsKey = string.Join(",", Node.DataTopics) + "|gen=" + AppState.BridgedDataCache.BridgeGeneration;
        if (topicsKey == _watcherTopicsKey) return;
        _watcherTopicsKey = topicsKey;

        foreach (var w in _dataWatchers) w.Dispose();
        _dataWatchers.Clear();

        var topics = Node.DataTopics.Count > 0
            ? Node.DataTopics.Cast<string?>().ToList()
            : new List<string?>();

        // Size the runtime arrays to match the topic list.
        Node.DataValues       = new object?[topics.Count];
        Node.DataUpdatedTimes = new DateTime?[topics.Count];

        for (int i = 0; i < topics.Count; i++)
        {
            var topic = topics[i];
            if (string.IsNullOrEmpty(topic)) continue;
            var idx = i;
            var capturedTopic = topic;

            var v = AppState.BridgedDataCache.GetValue(capturedTopic);
            if (v != null)
            {
                Node.DataValues[idx]       = v;
                Node.DataUpdatedTimes[idx] = DateTime.Now;
                if (idx == 0) { OnDataUpdated(); }
            }

            var watcher = AppState.BridgedDataCache.Subscribe(capturedTopic, async sub =>
            {
                if (sub.Status.IsPending) return;
                if (_disposed) return;
                var key = sub.Key;
                var value = (object?)sub.Value;
                await InvokeAsync(() =>
                {
                    if (_disposed) return;
                    Node.DataValues[idx]       = value;
                    Node.DataUpdatedTimes[idx] = DateTime.Now;
                    OnDataReceivedCore(idx, key, value);
                    OnDataUpdated();
                    StateHasChanged();
                });
            });
            _dataWatchers.Add(watcher);
        }
    }

    /// <summary>
    /// Called for every topic index when a value is received.
    /// </summary>
    protected virtual void OnDataReceivedCore(int index, string topic, object? rawValue) { }

    /// <summary>Called after any DataValue is updated. Override to react.</summary>
    protected virtual void OnDataUpdated() { }

    // ── Title positioning helpers ─────────────────────────────────────────────────
    // Used by widgets that position a title relative to their visual content.
    // Moves the four identical private copies out of individual widget classes.

    protected string TitlePos =>
        string.IsNullOrEmpty(Node.TitlePosition) ? "Above" : Node.TitlePosition;

    protected bool ShowTitleFirst() =>
        (TitlePos == "Above" || TitlePos == "Left") && !string.IsNullOrEmpty(Node.Title);

    protected string OuterFlexStyle() => TitlePos switch
    {
        "Left"  => "display:flex;flex-direction:row;align-items:center;height:100%;",
        "Right" => "display:flex;flex-direction:row;align-items:center;height:100%;",
        _       => "display:flex;flex-direction:column;height:100%;"
    };

    protected string TitleDivStyle() => TitlePos switch
    {
        "Left" or "Right" => "text-align:center;font-size:0.75rem;font-weight:500;padding:2px 4px;max-width:4rem;word-wrap:break-word;",
        _ => "text-align:center;font-size:0.75rem;font-weight:500;padding:2px 4px 0;"
    };

    /// <summary>
    /// Formats <see cref="TextNodeModel.Text"/> using data values as positional args:
    /// {0} = DataValues[0], {1} = DataValues[1], etc. Supports C# format specifiers
    /// e.g. "Temp: {0:F1}°C". Returns the raw Text if no format tokens are present or on error.
    /// </summary>
    private static readonly Regex _formatTokenRegex =
        new(@"\{(\d+)(?:,(-?\d+))?(?::([^}]*))?\}", RegexOptions.Compiled);

    protected string FormatTextCore(bool htmlEncode)
    {
        if (string.IsNullOrEmpty(Node.Text)) return string.Empty;
        try
        {
            var sb = new System.Text.StringBuilder();
            int lastIndex = 0;
            foreach (Match m in _formatTokenRegex.Matches(Node.Text))
            {
                var staticSegment = Node.Text[lastIndex..m.Index];
                sb.Append(htmlEncode ? System.Net.WebUtility.HtmlEncode(staticSegment) : staticSegment);

                int idx = int.Parse(m.Groups[1].Value);
                string? alignStr = m.Groups[2].Success && m.Groups[2].Length > 0 ? m.Groups[2].Value : null;
                string? spec = m.Groups[3].Success && m.Groups[3].Length > 0 ? m.Groups[3].Value : null;
                object? raw = idx < Node.DataValues.Length ? Node.DataValues[idx] : null;

                string formatted;
                try
                {
                    // Reconstruct standard C# format token format: {0,align:spec}
                    string tokenFormat = "{0";
                    if (alignStr != null) tokenFormat += "," + alignStr;
                    if (spec != null) tokenFormat += ":" + spec;
                    tokenFormat += "}";

                    formatted = string.Format(tokenFormat, new FormattableValue(raw));

                    // Truncation logic if alignment is set
                    if (alignStr != null && int.TryParse(alignStr, out var align) && align != 0)
                    {
                        var absAlign = Math.Abs(align);
                        if (formatted.Length > absAlign)
                        {
                            if (align < 0)
                            {
                                // Left aligned: truncate from right
                                formatted = formatted[..absAlign];
                            }
                            else
                            {
                                // Right aligned: keep rightmost characters
                                formatted = formatted.Substring(formatted.Length - absAlign);
                            }
                        }
                    }
                }
                catch
                {
                    formatted = raw?.ToString() ?? string.Empty;
                }

                sb.Append(htmlEncode ? System.Net.WebUtility.HtmlEncode(formatted) : formatted);
                lastIndex = m.Index + m.Length;
            }
            var trailingSegment = Node.Text[lastIndex..];
            sb.Append(htmlEncode ? System.Net.WebUtility.HtmlEncode(trailingSegment) : trailingSegment);
            return sb.ToString();
        }
        catch
        {
            return htmlEncode ? System.Net.WebUtility.HtmlEncode(Node.Text) : Node.Text;
        }
    }

    /// <summary>
    /// Formats <see cref="TextNodeModel.Text"/> using data values as positional args:
    /// {0} = DataValues[0], {1} = DataValues[1], etc. Supports C# format specifiers
    /// e.g. "Temp: {0:F1}°C". Returns the raw Text if no format tokens are present or on error.
    /// </summary>
    protected string FormatText() => FormatTextCore(htmlEncode: false);

    /// <summary>
    /// Renders <see cref="TextNodeModel.Text"/> substituting data values for <c>{0}</c>,
    /// <c>{1}</c>, etc. Both the static template and substituted values are HTML-encoded,
    /// so angle brackets and special characters appear as literal text.
    /// Newlines (Enter in the text field) are preserved as line breaks via CSS white-space:pre-wrap.
    /// Supports C# format specs, e.g. {0:F2}.
    /// </summary>
    protected MarkupString FormatHtml() => new MarkupString(FormatTextCore(htmlEncode: true));

    /// <summary>Wraps an arbitrary MQTT value for use with string.Format numeric format specifiers.</summary>
    private sealed class FormattableValue(object? value) : IFormattable
    {
        private readonly object? _value = value;

        public string ToString(string? format, IFormatProvider? provider)
        {
            try
            {
                if (format != null)
                {
                    switch (format[0])
                    {
                        case 'E': case 'F': case 'G': case 'N': case '0':
                            if (_value?.GetType() == typeof(string))
                            { if (double.TryParse(_value.ToString(), out double d)) return d.ToString(format, provider); }
                            else if (_value is int iv) return ((double)iv).ToString(format, provider);
                            break;
                        case 'I': case 'X':
                            if (_value?.GetType() == typeof(string))
                            { if (int.TryParse(_value.ToString(), out int i)) return i.ToString(format, provider); }
                            else if (_value is double dv) return ((int)dv).ToString(format, provider);
                            break;
                    }
                }
            }
            catch { }
            if (_value == null) return "";
            return (_value as IFormattable)?.ToString(format, provider) ?? (_value.ToString() ?? "");
        }
    }

    public override void Dispose()
    {
        _disposed = true;
        _watcherTopicsKey = null;
        foreach (var w in _dataWatchers) w.Dispose();
        _dataWatchers.Clear();
        base.Dispose();
    }
}
