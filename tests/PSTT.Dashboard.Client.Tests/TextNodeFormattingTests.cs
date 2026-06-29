using Xunit;
using PSTT.Dashboard.Models;
using PSTT.Dashboard.Widgets;
using PSTT.Dashboard.Services;

namespace PSTT.Dashboard.Client.Tests
{
    public class TextNodeFormattingTests
    {
        private class TestWidget : BaseNodeWithDataWidget<TextNodeModel>
        {
            public TestWidget(TextNodeModel node, ApplicationState appState)
            {
                Node = node;
                AppState = appState;
            }

            public string TestFormatText() => FormatText();
            public string TestFormatHtml() => FormatHtml().Value;
        }

        [Fact]
        public void FormatText_Simple_NoAlignment_Works()
        {
            var node = new TextNodeModel();
            node.Text = "Temp: {0:F1}°C";
            node.DataValues = new[] { "42.537" };
            var appState = new ApplicationState();

            var widget = new TestWidget(node, appState);
            Assert.Equal("Temp: 42.5°C", widget.TestFormatText());
            Assert.Equal("Temp: 42.5&#176;C", widget.TestFormatHtml());
        }

        [Fact]
        public void FormatText_WithAlignment_Works()
        {
            var node = new TextNodeModel();
            node.Text = "val: '{0,-10}'";
            node.DataValues = new[] { "hello" };
            var appState = new ApplicationState();

            var widget = new TestWidget(node, appState);
            Assert.Equal("val: 'hello     '", widget.TestFormatText());
        }

        [Fact]
        public void FormatText_WithAlignmentAndTruncation_Works()
        {
            var node = new TextNodeModel();
            node.Text = "val: '{0,-5}'";
            node.DataValues = new[] { "longerstring" };
            var appState = new ApplicationState();

            var widget = new TestWidget(node, appState);
            Assert.Equal("val: 'longe'", widget.TestFormatText());
        }

        [Fact]
        public void FormatText_WithRightAlignmentAndTruncation_Works()
        {
            var node = new TextNodeModel();
            node.Text = "val: '{0,5}'";
            node.DataValues = new[] { "longerstring" };
            var appState = new ApplicationState();

            var widget = new TestWidget(node, appState);
            // Right aligned: keeps rightmost 5 characters
            Assert.Equal("val: 'tring'", widget.TestFormatText());
        }

        [Fact]
        public void FormatHtml_EscapesHtml_Works()
        {
            var node = new TextNodeModel();
            node.Text = "val: <{0}>";
            node.DataValues = new[] { "hello & world" };
            var appState = new ApplicationState();

            var widget = new TestWidget(node, appState);
            Assert.Equal("val: &lt;hello &amp; world&gt;", widget.TestFormatHtml());
        }
    }
}
