using Blazor.Diagrams.Core.Anchors;
using Blazor.Diagrams.Core.Models;
using Microsoft.AspNetCore.Components;
using MqttDashboard.Models;

namespace MqttDashboard.Widgets;

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
        if (firstRender)
        {
            // Ensure link animations are applied once the SVG is in the DOM.
            // Node widgets only mount after IsInteractive = true (guarded by @if in Display.razor),
            // so firstRender reliably fires when the diagram canvas is rendered.
            TriggerLinkAnimation();
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    protected void SetupDataWatchers()
    {
        // Skip rebuild if topics haven't changed — prevents redundant re-runs
        // from every OnParametersSet (triggered by RefreshAll, StateHasChanged, etc.).
        var topicsKey = string.Join(",", Node.DataTopics);
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

            var v = AppState.DataCache.GetValue(capturedTopic);
            if (v != null)
            {
                Node.DataValues[idx]       = v;
                Node.DataUpdatedTimes[idx] = DateTime.Now;
                if (idx == 0) { OnDataUpdated(); TriggerLinkAnimation(); }
            }

            var watcher = AppState.DataCache.Subscribe(capturedTopic, (t, value) =>
            {
                if (_disposed) return;
                // Marshal ALL render-affecting work onto the renderer's synchronisation context.
                // Blazor Diagrams' l.Refresh() fires Changed events that cause DiagramCanvas to
                // call StateHasChanged() — invoking this from an MQTT background thread throws
                // InvalidOperationException which corrupts and eventually terminates the circuit.
                _ = InvokeAsync(() =>
                {
                    if (_disposed) return;
                    Node.DataValues[idx]       = value;
                    Node.DataUpdatedTimes[idx] = DateTime.Now;
                    OnDataReceivedCore(idx, t, value);//with values
                    OnDataUpdated();
                    if (idx == 0)
                    {
                        TriggerLinkAnimation();
                    }
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

    /// <summary>
    /// Updates link animation direction on all outgoing links based on the current DataValue
    /// and the node's LinkAnimation setting. Runs automatically on every data update.
    /// </summary>
    protected void TriggerLinkAnimation()
    {
        if (Node.LinkAnimation == null || Node.LinkAnimation == "None") return;
        var val = Node.DataValues?[0]?.ToString();
        if (val == null || !double.TryParse(val, out var d)) return;

        if (Node.LinkAnimation == "Reverse") d = -d;

        foreach (var port in Node.Ports)
        {
            foreach (var link in port.Links)
            {
                if (link is not LinkModel l || l.Animations == null || l.Animations[0] == null) continue;
                var ani = l.Animations[0];
                var anchor = link.Source as SinglePortAnchor;
                if (anchor?.Port != port) continue;

                var to = d > 0 ? "-10" : d < 0 ? "10" : "0";
                if (to != ani.To)
                {
                    ani.To = to;
                    l.Refresh();
                }
            }
        }
    }

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
    protected string FormatText()
    {
        if (string.IsNullOrEmpty(Node.Text)) return string.Empty;
        try
        {
            return string.Format(Node.Text,
                Node.DataValues.Length > 0 ? new FormattableValue(Node.DataValues[0]) : null,
                Node.DataValues.Length > 1 ? new FormattableValue(Node.DataValues[1]) : null,
                Node.DataValues.Length > 2 ? new FormattableValue(Node.DataValues[2]) : null,
                Node.DataValues.Length > 3 ? new FormattableValue(Node.DataValues[3]) : null,
                null);
        }
        catch { return Node.Text; }
    }

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
