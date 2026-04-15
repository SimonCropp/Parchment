using Markdig.Syntax;

public class ListBlockRendererTests
{
    [Test]
    public async Task BulletListProducesListParagraphsWithNumbering()
    {
        var paragraphs = RenderList("- one\n- two\n- three");

        await Assert.That(paragraphs.Count).IsEqualTo(3);
        foreach (var p in paragraphs)
        {
            await Assert.That(p.ParagraphProperties!.ParagraphStyleId!.Val?.Value).IsEqualTo("ListParagraph");
            var num = p.ParagraphProperties.NumberingProperties!;
            await Assert.That(num.NumberingLevelReference!.Val?.Value).IsEqualTo(0);
            await Assert.That(num.NumberingId!.Val?.Value).IsNotNull();
        }

        var texts = paragraphs.Select(p => p.GetFirstChild<Run>()!.GetFirstChild<Text>()!.Text).ToList();
        await Assert.That(texts).IsEquivalentTo(["one", "two", "three"]);
    }

    [Test]
    public async Task OrderedListProducesNumberingInstance()
    {
        var paragraphs = RenderList("1. first\n2. second");
        await Assert.That(paragraphs.Count).IsEqualTo(2);

        var numId = paragraphs[0].ParagraphProperties!.NumberingProperties!.NumberingId!.Val?.Value;
        await Assert.That(numId).IsNotNull();
    }

    static List<Paragraph> RenderList(string markdown)
    {
        var list = RendererHarness.FirstBlock<ListBlock>(markdown);
        var renderer = RendererHarness.BuildRenderer();
        renderer.Render(list);
        return renderer.Drain().Cast<Paragraph>().ToList();
    }
}
