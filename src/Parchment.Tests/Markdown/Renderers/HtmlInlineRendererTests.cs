namespace Parchment.Tests.Markdown.Renderers;

using global::Markdig.Syntax.Inlines;

public class HtmlInlineRendererTests
{
    [Test]
    public async Task EmitsRunWithTagText()
    {
        var renderer = RendererHarness.BuildRenderer();
        renderer.Render(new HtmlInline("<br/>"));

        var run = (Run)renderer.Top.CurrentRuns.Single();
        await Assert.That(run.GetFirstChild<Text>()!.Text).IsEqualTo("<br/>");
    }

    [Test]
    public async Task EmptyTagEmitsNothing()
    {
        var renderer = RendererHarness.BuildRenderer();
        renderer.Render(new HtmlInline(string.Empty));

        await Assert.That(renderer.Top.CurrentRuns.Count).IsEqualTo(0);
    }
}
