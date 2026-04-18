using Blazor.Diagrams.Core.Models;
using Blazor.Diagrams.Core.Geometry;

namespace MqttDashboard.Models
{
    public class NodePortModel : PortModel
    {
        public NodePortModel(NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(parent, alignment, position, size)
        {
            Init();
        }

        public NodePortModel(string id, NodeModel parent, PortAlignment alignment = PortAlignment.Bottom, Point? position = null, Size? size = null) : base(id, parent, alignment, position, size)
        {
            Init();
        }

        void Init()
        {
            Size = new Size(10,10);
        }
    }
}
