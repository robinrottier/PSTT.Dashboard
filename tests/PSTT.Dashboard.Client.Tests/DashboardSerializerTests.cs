using System.Text.Json;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Serialization;

namespace PSTT.Dashboard.Client.Tests;

public class DashboardSerializerTests
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ── DashboardIdMapper ─────────────────────────────────────────────────────

    [Fact]
    public void Mapper_FirstOccurrence_AssignsOne()
    {
        var m = new DashboardIdMapper();
        Assert.Equal("1", m.Map("abc-guid"));
    }

    [Fact]
    public void Mapper_SecondDistinct_AssignsTwo()
    {
        var m = new DashboardIdMapper();
        m.Map("first");
        Assert.Equal("2", m.Map("second"));
    }

    [Fact]
    public void Mapper_SameId_ReturnsSameMappedValue()
    {
        var m = new DashboardIdMapper();
        var a = m.Map("shared");
        var b = m.Map("shared");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Mapper_EmptyId_PassesThrough()
    {
        var m = new DashboardIdMapper();
        Assert.Equal(string.Empty, m.Map(string.Empty));
    }

    // ── DashboardSerializer.Serialize — node IDs ──────────────────────────────

    private static DashboardModel SingleNodeModel(string nodeId, string? portId = null)
    {
        var node = new TextNodeData { Id = nodeId };
        if (portId is not null)
            node.Ports = [new NodePortData { Id = portId, Alignment = "Right" }];

        return new DashboardModel
        {
            Name = "Test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "page-guid-1",
                    Nodes = [node],
                    Links = []
                }
            ]
        };
    }

    [Fact]
    public void Serialize_NodeId_ReplacedWithSequentialId()
    {
        var model = SingleNodeModel("node-guid-abc");
        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        // Page Id is visited first (declaration order); node Id second
        Assert.Equal("1", loaded.Pages[0].Id);
        Assert.Equal("2", loaded.Pages[0].Nodes[0].Id);
    }

    [Fact]
    public void Serialize_OriginalModelNotMutated()
    {
        const string nodeId = "original-node-guid";
        var model = SingleNodeModel(nodeId);
        DashboardSerializer.Serialize(model, WriteOptions);

        Assert.Equal(nodeId, model.Pages[0].Nodes[0].Id);
    }

    [Fact]
    public void Serialize_PortId_IsSequentialAfterNodeId()
    {
        var model = SingleNodeModel("node-guid", "port-guid");
        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        // page=1, node=2, port=3
        Assert.Equal("1", loaded.Pages[0].Id);
        Assert.Equal("2", loaded.Pages[0].Nodes[0].Id);
        Assert.Equal("3", loaded.Pages[0].Nodes[0].Ports![0].Id);
    }

    // ── Link cross-references ─────────────────────────────────────────────────

    [Fact]
    public void Serialize_LinkSourceTarget_MatchRemappedNodeIds()
    {
        var nodeA = new TextNodeData { Id = "guid-A", Ports = [new NodePortData { Id = "port-A", Alignment = "Right" }] };
        var nodeB = new TextNodeData { Id = "guid-B", Ports = [new NodePortData { Id = "port-B", Alignment = "Left" }] };

        var model = new DashboardModel
        {
            Name = "Link test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "page-1",
                    Nodes = [nodeA, nodeB],
                    Links =
                    [
                        new LinkData
                        {
                            Source = "guid-A", SourcePort = "port-A",
                            Target = "guid-B", TargetPort = "port-B"
                        }
                    ]
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var page = loaded.Pages[0];
        var nodeALoaded = page.Nodes[0];
        var nodeBLoaded = page.Nodes[1];
        var link = page.Links[0];

        // IDs must be internally consistent
        Assert.Equal(link.Source, nodeALoaded.Id);
        Assert.Equal(link.Target, nodeBLoaded.Id);
        Assert.Equal(link.SourcePort, nodeALoaded.Ports![0].Id);
        Assert.Equal(link.TargetPort, nodeBLoaded.Ports![0].Id);
    }

    [Fact]
    public void Serialize_NullPortIds_PassThroughUnchanged()
    {
        var model = new DashboardModel
        {
            Name = "No ports",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p",
                    Nodes =
                    [
                        new TextNodeData { Id = "n1" },
                        new TextNodeData { Id = "n2" }
                    ],
                    Links =
                    [
                        new LinkData { Source = "n1", Target = "n2" }  // no SourcePort / TargetPort
                    ]
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;
        var link = loaded.Pages[0].Links[0];

        Assert.Null(link.SourcePort);
        Assert.Null(link.TargetPort);
    }

    // ── Multi-page counter continuity ─────────────────────────────────────────

    [Fact]
    public void Serialize_MultiPage_CounterContinuesAcrossPages()
    {
        var model = new DashboardModel
        {
            Name = "Multi page",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "pg1",
                    Nodes = [new TextNodeData { Id = "n1" }],
                    Links = []
                },
                new DashboardPageModel
                {
                    Id = "pg2",
                    Nodes = [new TextNodeData { Id = "n2" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        // pg1=1, n1=2, pg2=3, n2=4  (IDs are unique across pages)
        var id1 = loaded.Pages[0].Id;
        var id2 = loaded.Pages[0].Nodes[0].Id;
        var id3 = loaded.Pages[1].Id;
        var id4 = loaded.Pages[1].Nodes[0].Id;

        Assert.Equal(["1","2","3","4"], new[] { id1, id2, id3, id4 });
    }

    // ── FileInfo not remapped ─────────────────────────────────────────────────

    [Fact]
    public void Serialize_FileInfoStrings_NotRemapped()
    {
        var model = new DashboardModel
        {
            Name = "Stamped",
            Pages = [],
            FileInfo = new DashboardFileInfo
            {
                WrittenAt = "2026-01-01T00:00:00Z",
                Filename = "test.json",
                AppVersion = "1.2.3",
                WrittenByServer = "myhost"
            }
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        Assert.Equal("2026-01-01T00:00:00Z", loaded.FileInfo!.WrittenAt);
        Assert.Equal("test.json", loaded.FileInfo.Filename);
        Assert.Equal("1.2.3", loaded.FileInfo.AppVersion);
        Assert.Equal("myhost", loaded.FileInfo.WrittenByServer);
    }

    // ── Polymorphic NodeData survives round-trip ──────────────────────────────

    [Fact]
    public void Serialize_PolymorphicNodes_TypesPreserved()
    {
        var model = new DashboardModel
        {
            Name = "Poly",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p",
                    Nodes =
                    [
                        new GaugeNodeData { Id = "g1", Unit = "°C" },
                        new SwitchNodeData { Id = "s1", Switch = new SwitchSettingsData { PublishTopic = "cmd/switch" } },
                        new LogNodeData { Id = "l1", MaxEntries = 50 }
                    ],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        Assert.IsType<GaugeNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.IsType<SwitchNodeData>(loaded.Pages[0].Nodes[1]);
        Assert.IsType<LogNodeData>(loaded.Pages[0].Nodes[2]);

        Assert.Equal("°C", ((GaugeNodeData)loaded.Pages[0].Nodes[0]).Unit);
        Assert.Equal(50, ((LogNodeData)loaded.Pages[0].Nodes[2]).MaxEntries);
    }

    // ── Round-trip: Deserialize ───────────────────────────────────────────────

    [Fact]
    public void Deserialize_SequentialIds_LoadedCorrectly()
    {
        // Simulates loading a file that was previously saved with sequential IDs.
        // Uses PascalCase property names as written by the serializer (no camelCase policy).
        const string json = """
            {
              "Name":"RT",
              "Pages":[{
                "Id":"1",
                "Name":"Page 1",
                "GridSize":20,
                "GridSnapToCenter":false,
                "Nodes":[{"nodeType":"Text","Id":"2","X":0,"Y":0,"Width":100,"Height":50}],
                "Links":[]}]
            }
            """;

        var model = DashboardSerializer.Deserialize(json);

        Assert.NotNull(model);
        Assert.Equal("1", model!.Pages[0].Id);
        Assert.Equal("2", model.Pages[0].Nodes[0].Id);
    }

    // ── SerializePage helper ──────────────────────────────────────────────────

    [Fact]
    public void SerializePage_WrapsInEnvelope_WithRemappedIds()
    {
        var page = new DashboardPageModel
        {
            Id = "page-guid",
            Nodes = [new TextNodeData { Id = "node-guid" }],
            Links = []
        };

        var json = DashboardSerializer.SerializePage(page, WriteOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("page", root.GetProperty("psttdashboard").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("1", data.GetProperty("Id").GetString());
        Assert.Equal("2", data.GetProperty("Nodes")[0].GetProperty("Id").GetString());
    }

    [Fact]
    public void SerializePage_OriginalNotMutated()
    {
        const string pageId = "original-page-guid";
        var page = new DashboardPageModel { Id = pageId, Nodes = [], Links = [] };
        DashboardSerializer.SerializePage(page, WriteOptions);
        Assert.Equal(pageId, page.Id);
    }

    // ── SerializeNodes helper ─────────────────────────────────────────────────

    [Fact]
    public void SerializeNodes_WrapsInEnvelope_WithRemappedIds()
    {
        var nodes = new List<NodeData>
        {
            new TextNodeData { Id = "guid-1", Ports = [new NodePortData { Id = "port-1", Alignment = "Right" }] },
            new TextNodeData { Id = "guid-2" }
        };

        var json = DashboardSerializer.SerializeNodes(nodes, WriteOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("nodes", root.GetProperty("psttdashboard").GetString());
        var data = root.GetProperty("data");
        Assert.Equal("1", data[0].GetProperty("Id").GetString());
        Assert.Equal("2", data[0].GetProperty("Ports")[0].GetProperty("Id").GetString());
        Assert.Equal("3", data[1].GetProperty("Id").GetString());
    }

    // ── FEAT-C new node types round-trip ─────────────────────────────────────

    [Fact]
    public void Serialize_SliderNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "Slider test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new SliderNodeData { Id = "s1", Min = 0, Max = 50, Step = 0.5, Unit = "%", PublishTopic = "cmd/slider" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var node = Assert.IsType<SliderNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal(0, node.Min);
        Assert.Equal(50, node.Max);
        Assert.Equal(0.5, node.Step);
        Assert.Equal("%", node.Unit);
        Assert.Equal("cmd/slider", node.PublishTopic);
        // ID was remapped
        Assert.Equal("2", node.Id);
    }

    [Fact]
    public void Serialize_ButtonNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "Button test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new ButtonNodeData { Id = "b1", ButtonLabel = "Press me", PublishValue = "1", PublishTopic = "cmd/btn", ButtonVariant = "Filled", ButtonColor = "Primary" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var node = Assert.IsType<ButtonNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal("Press me", node.ButtonLabel);
        Assert.Equal("1", node.PublishValue);
        Assert.Equal("cmd/btn", node.PublishTopic);
        Assert.Equal("Filled", node.ButtonVariant);
        Assert.Equal("Primary", node.ButtonColor);
        Assert.Equal("2", node.Id);
    }

    [Fact]
    public void Serialize_HtmlNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "HTML test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new HtmlNodeData { Id = "h1" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        Assert.IsType<HtmlNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal("2", loaded.Pages[0].Nodes[0].Id);
    }

    [Fact]
    public void Serialize_IFrameNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "IFrame test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new IFrameNodeData { Id = "i1", SourceUrl = "https://example.com" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var node = Assert.IsType<IFrameNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal("https://example.com", node.SourceUrl);
        Assert.Equal("2", node.Id);
    }

    [Fact]
    public void Serialize_AllFeatCTypes_TypesPreserved()
    {
        var model = new DashboardModel
        {
            Name = "All FEAT-C",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes =
                    [
                        new SliderNodeData { Id = "s1", Max = 100 },
                        new ButtonNodeData { Id = "b1", ButtonLabel = "Click" },
                        new HtmlNodeData { Id = "h1" },
                        new IFrameNodeData { Id = "i1", SourceUrl = "https://example.com" }
                    ],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;
        var nodes = loaded.Pages[0].Nodes;

        Assert.IsType<SliderNodeData>(nodes[0]);
        Assert.IsType<ButtonNodeData>(nodes[1]);
        Assert.IsType<HtmlNodeData>(nodes[2]);
        Assert.IsType<IFrameNodeData>(nodes[3]);

        Assert.Equal(100, ((SliderNodeData)nodes[0]).Max);
        Assert.Equal("Click", ((ButtonNodeData)nodes[1]).ButtonLabel);
        Assert.Equal("https://example.com", ((IFrameNodeData)nodes[3]).SourceUrl);
    }

    [Fact]
    public void Serialize_TextEntryNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "TextEntry test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new TextEntryNodeData { Id = "te1", Placeholder = "Enter value", PublishTopic = "home/text", IsReadOnly = false, Retain = true, PublishGlobally = true }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var node = Assert.IsType<TextEntryNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal("Enter value", node.Placeholder);
        Assert.Equal("home/text", node.PublishTopic);
        Assert.True(node.Retain);
        Assert.True(node.PublishGlobally);
        Assert.Equal("2", node.Id);
    }

    [Fact]
    public void Serialize_DropDownNodeData_RoundTrip()
    {
        var model = new DashboardModel
        {
            Name = "DropDown test",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "p1",
                    Nodes = [new DropDownNodeData { Id = "dd1", Options = "On,Off,Auto", PublishTopic = "home/mode", IsReadOnly = false, Retain = false, PublishGlobally = false }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.Serialize(model, WriteOptions);
        var loaded = JsonSerializer.Deserialize<DashboardModel>(json)!;

        var node = Assert.IsType<DropDownNodeData>(loaded.Pages[0].Nodes[0]);
        Assert.Equal("On,Off,Auto", node.Options);
        Assert.Equal("home/mode", node.PublishTopic);
        Assert.False(node.Retain);
        Assert.False(node.PublishGlobally);
        Assert.Equal("2", node.Id);
    }

    // ── SerializeDashboard helper ─────────────────────────────────────────────


    [Fact]
    public void SerializeDashboard_WrapsInEnvelope_WithRemappedIds()
    {
        var model = new DashboardModel
        {
            Name = "MyDash",
            Pages =
            [
                new DashboardPageModel
                {
                    Id = "pg-guid",
                    Nodes = [new TextNodeData { Id = "nd-guid" }],
                    Links = []
                }
            ]
        };

        var json = DashboardSerializer.SerializeDashboard(model, WriteOptions);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("dashboard", root.GetProperty("psttdashboard").GetString());
        var page = root.GetProperty("data").GetProperty("Pages")[0];
        Assert.Equal("1", page.GetProperty("Id").GetString());
        Assert.Equal("2", page.GetProperty("Nodes")[0].GetProperty("Id").GetString());
    }
}
