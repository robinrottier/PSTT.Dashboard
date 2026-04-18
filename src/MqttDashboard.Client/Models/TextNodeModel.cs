
namespace MqttDashboard.Models
{
    public class TextNodeModel : Blazor.Diagrams.Core.Models.NodeModel
    {
        public TextNodeModel(Blazor.Diagrams.Core.Geometry.Point? position = null) : base(position)
        {
            // Disable the Blazor.Diagrams ResizeObserver for all our nodes.
            // We set explicit CSS sizes (width/height) on every node and manage them ourselves
            // via OnInitialized in each widget and via the resize handle drag. Leaving
            // ControlledSize=false (the default) means NodeRenderer sets up a JS ResizeObserver
            // that calls OnResize(getBoundingClientRect()) after each render — but getBoundingClientRect
            // includes any sub-pixel rounding or zoom-division noise, so the reported size can differ
            // slightly from the stored size, triggering a re-render, which re-fires the observer, causing
            // the node to grow or shrink indefinitely.
            //
            // C# init semantics: ControlledSize is declared as { get; init; } in NodeModel (rrSoft.Blazor.Diagrams
            // 0.1.2). Setting it here works because the C# spec allows derived-class constructors to call
            // init accessors of base-class properties (they share the same "init context").
            // If the library ever changes ControlledSize to { get; protected set; }, this line still
            // compiles unchanged; if it becomes virtual, we could override it instead.
            ControlledSize = true;
        }
        /// <summary>
        /// Position of the title relative to the main content: "Above", "Below", "Left", "Right". Defaults to "Above".
        /// (Title is inherited from base blazor diagram node model)
        /// </summary>
        public string TitlePosition { get; set; } = "Above";

        /// <summary>Icon name from MudBlazor Icons (e.g., Icons.Material.Filled.Home)</summary>
        public string? Icon { get; set; }

        /// <summary>Human-readable icon name for display</summary>
        public string? IconName { get; set; }

        /// <summary>Icon color</summary>
        public string? IconColor { get; set; }

        /// <summary>
        /// Format string for the body text. Use {0} for first data value, {1} for second, etc.
        /// Supports C# format specifiers e.g. "Temp: {0:F2}°C\nHumidity: {1:F1}%"
        /// </summary>
        public string? Text { get; set; }

        /// <summary>Background color for the node</summary>
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Static background image URL (http/https/data URI).
        /// Displayed as the node background behind all content.
        /// </summary>
        public string? BackgroundImageUrl { get; set; }

        /// <summary>
        /// How the background image fills the node container.
        /// CSS background-size value: "cover", "contain", or "100% 100%" (fill/stretch).
        /// Defaults to "cover".
        /// </summary>
        public string BackgroundObjectFit { get; set; } = "cover";

        /// <summary>Custom metadata dictionary for future extensibility</summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>List of MQTT topics for data binding</summary>
        public List<string> DataTopics { get; set; } = new();
        
        // Runtime-only arrays: populated by MQTT watchers, never serialised.
        // Length is set by BaseNodeWithDataWidget to match DataTopics.Count.
        public object?[]   DataValues      { get; set; } = Array.Empty<object?>();
        public DateTime?[] DataUpdatedTimes { get; set; } = Array.Empty<DateTime?>();

        // Computed convenience accessors — these are read-only; set via DataTopics list.
        public string? DataTopic(int n) => DataTopics.Count > n ? DataTopics[n] : null;
        public object? DataValue(int n) => DataValues.Length > n ? DataValues[n] : null;

        /// <summary>Optional font size in pixels for data values</summary>
        public int? FontSize { get; set; }

        /// <summary>Link animation style for links sourced from this node: "None", "Forward", "Reverse"</summary>
        public string? LinkAnimation { get; set; }

        /// <summary>Node type discriminator. Defaults to "Text" (existing text/display node).</summary>
        public string NodeType { get; set; } = "Text";

        // ── Serialization helpers ──────────────────────────────────────────────

        protected void FillBaseData(NodeData data, double panX, double panY)
        {
            data.Id = Id;
            data.Title = Title;
            data.X = (Position?.X ?? 0) + panX;
            data.Y = (Position?.Y ?? 0) + panY;
            data.Width = Size?.Width ?? 120;
            data.Height = Size?.Height ?? 90;
            data.Icon = Icon;
            data.IconName = IconName;
            data.IconColor = IconColor;
            data.Text = Text;
            data.BackgroundColor = BackgroundColor;
            data.BackgroundImageUrl = string.IsNullOrEmpty(BackgroundImageUrl) ? null : BackgroundImageUrl;
            data.BackgroundObjectFit = BackgroundObjectFit != "cover" ? BackgroundObjectFit : null;
            data.TitlePosition = TitlePosition != "Above" ? TitlePosition : null;
            data.LinkAnimation = LinkAnimation;
            data.FontSize = FontSize;
            data.Metadata = Metadata.Count > 0 ? new Dictionary<string, string>(Metadata) : null;
            data.DataTopics = DataTopics.Count > 0 ? new List<string>(DataTopics) : null;
            data.Ports = Ports.Any()
                ? Ports.Select(p => new NodePortData { Id = p.Id, Alignment = p.Alignment.ToString() }).ToList()
                : null;
        }

        protected static T ApplyBaseData<T>(T node, NodeData data) where T : TextNodeModel
        {
            node.Title = data.Title;
            node.Size = new Blazor.Diagrams.Core.Geometry.Size(data.Width > 0 ? data.Width : 120, data.Height > 0 ? data.Height : 90);
            node.Icon = data.Icon;
            node.IconName = data.IconName;
            node.IconColor = data.IconColor;
            node.Text = data.Text;
            node.BackgroundColor = data.BackgroundColor;
            node.BackgroundImageUrl = data.BackgroundImageUrl ?? string.Empty;
            node.BackgroundObjectFit = data.BackgroundObjectFit ?? "cover";
            node.TitlePosition = data.TitlePosition ?? "Above";
            node.LinkAnimation = data.LinkAnimation;
            node.FontSize = data.FontSize;
            node.Metadata = data.Metadata ?? new Dictionary<string, string>();
            node.DataTopics = data.DataTopics != null ? new List<string>(data.DataTopics) : new List<string>();
            return node;
        }

        public virtual NodeData ToData(double panX = 0, double panY = 0)
        {
            var data = new TextNodeData();
            FillBaseData(data, panX, panY);
            return data;
        }

        public static TextNodeModel FromData(NodeData data)
        {
            var node = new TextNodeModel(new Blazor.Diagrams.Core.Geometry.Point(data.X, data.Y));
            return ApplyBaseData(node, data);
        }
    }
}
