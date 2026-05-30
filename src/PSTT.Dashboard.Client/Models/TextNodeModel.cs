
namespace PSTT.Dashboard.Models
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
        /// <summary>Wrapper so the grid view can edit Title via NpXxx attribute discovery.</summary>
        [NpText("Title", Category = "Common", Order = 0)]
        public string NodeTitle { get => Title ?? ""; set => Title = value; }

        [NpSelect("Title Position", "Above", "Below", "Left", "Right", Category = "Common", Order = 1, Labels = ["Above", "Below", "Left", "Right"])]
        public string TitlePosition { get; set; } = "Above";

        /// <summary>Icon name from MudBlazor Icons (e.g., Icons.Material.Filled.Home)</summary>
        public string? Icon { get; set; }

        /// <summary>Human-readable icon name for display</summary>
        public string? IconName { get; set; }

        /// <summary>Icon color</summary>
        [NpColor("Icon Color", Category = "Common", Order = 3, ShowClear = true)]
        public string? IconColor { get; set; }

        /// <summary>
        /// Format string for the body text. Use {0} for first data value, {1} for second, etc.
        /// Supports C# format specifiers e.g. "Temp: {0:F2}°C\nHumidity: {1:F1}%"
        /// </summary>
        [NpText("Text", Category = "Common", Order = 4, Lines = 4, Placeholder = "Use {0} for data value")]
        public string? Text { get; set; }

        /// <summary>
        /// Returns <c>false</c> if this node type does not support editing the named property.
        /// Used by NodePropertyEditor to show/hide property fields.
        /// Override in derived models to suppress base-class properties irrelevant to the widget.
        /// </summary>
        public virtual bool SupportsProperty(string propertyName) => true;

        /// <summary>Background color for the node</summary>
        [NpColor("Background", Category = "Common", Order = 5, ShowClear = true)]
        public string? BackgroundColor { get; set; }

        /// <summary>
        /// Static background image URL (http/https/data URI).
        /// Displayed as the node background behind all content.
        /// </summary>
        [NpText("Background Image URL", Category = "Common", Order = 6)]
        public string? BackgroundImageUrl { get; set; }

        /// <summary>
        /// How the background image fills the node container.
        /// CSS background-size value: "cover", "contain", or "100% 100%" (fill/stretch).
        /// Defaults to "cover".
        /// </summary>
        [NpSelect("Background Fit", "cover", "contain", "100% 100%", Category = "Common", Order = 7, Labels = ["Cover", "Contain", "Stretch"])]
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
        [NpNumeric("Font Size", Category = "Common", Order = 8, Min = 0)]
        public int? FontSize { get; set; }

        /// <summary>Node type discriminator. Defaults to "Text" (existing text/display node).</summary>
        public string NodeType { get; set; } = "Text";

        /// <summary>
        /// Runtime-only: true when this node is the primary reference node in a multi-selection
        /// (used for same-width/height and format-painter operations). Never serialized.
        /// </summary>
        public bool IsPrimarySelection { get; set; }

        /// <summary>
        /// Copy visual formatting from <paramref name="source"/> into this node.
        /// Copies base properties (background, icon, text, font, title) but NOT DataTopics,
        /// Position, Size, NodeType, or Id.
        /// Override in derived classes to also copy type-specific formatting properties.
        /// </summary>
        public virtual void CopyFormatFrom(TextNodeModel source)
        {
            NodeTitle           = source.NodeTitle;
            TitlePosition       = source.TitlePosition;
            Icon                = source.Icon;
            IconName            = source.IconName;
            IconColor           = source.IconColor;
            Text                = source.Text;
            FontSize            = source.FontSize;
            BackgroundColor     = source.BackgroundColor;
            BackgroundImageUrl  = source.BackgroundImageUrl;
            BackgroundObjectFit = source.BackgroundObjectFit;
        }

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
            data.FontSize = FontSize;
            data.Metadata = Metadata.Count > 0 ? new Dictionary<string, string>(Metadata) : null;
            data.DataTopics = DataTopics.Count > 0 ? new List<string>(DataTopics) : null;
            data.Ports = Ports.Any()
                ? Ports.Select(p => new NodePortData { Id = p.Id, Alignment = p.Alignment.ToString(), PortStyle = (p as NodePortModel)?.PortStyle }).ToList()
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
