using MarkdigTable = Markdig.Extensions.Tables.Table;

public class TableRendererTests
{
    public class EmptyModel;

    [Test]
    public async Task PipeTableEmitsTableWithGridRowsAndHeaderFormatting()
    {
        const string md = """
                          | A | B |
                          |---|---|
                          | 1 | 2 |
                          | 3 | 4 |
                          """;

        var tableBlock = RendererHarness.FirstBlock<MarkdigTable>(md);
        var renderer = RendererHarness.BuildRenderer();

        renderer.Render(tableBlock);

        var table = (Table)renderer.Drain().Single();

        var grid = table.GetFirstChild<TableGrid>()!;
        await Assert.That(grid.Elements<GridColumn>().Count()).IsEqualTo(2);

        var rows = table.Elements<TableRow>().ToList();
        await Assert.That(rows.Count).IsEqualTo(3);

        var headerCells = rows[0].Elements<TableCell>().ToList();
        await Assert.That(headerCells.Count).IsEqualTo(2);
        var headerParagraph = headerCells[0].GetFirstChild<Paragraph>()!;
        await Assert.That(headerParagraph.ParagraphProperties!.GetFirstChild<Justification>()!.Val?.Value)
            .IsEqualTo(JustificationValues.Center);
        var headerRun = headerParagraph.GetFirstChild<Run>()!;
        await Assert.That(headerRun.RunProperties!.GetFirstChild<Bold>()).IsNotNull();

        var bodyCellRun = rows[1].Elements<TableCell>().First()
            .GetFirstChild<Paragraph>()!
            .GetFirstChild<Run>()!;
        await Assert.That(bodyCellRun.RunProperties?.GetFirstChild<Bold>()).IsNull();

        await VerifyDocument(md);
    }

    [Test]
    public async Task GridTableEmitsTableWithCorrectStructure()
    {
        const string md = """
                          +---+---+
                          | A | B |
                          +===+===+
                          | 1 | 2 |
                          +---+---+
                          | 3 | 4 |
                          +---+---+
                          """;

        var tableBlock = RendererHarness.FirstBlock<MarkdigTable>(md);
        var renderer = RendererHarness.BuildRenderer();

        renderer.Render(tableBlock);

        var table = (Table)renderer.Drain().Single();

        var grid = table.GetFirstChild<TableGrid>()!;
        await Assert.That(grid.Elements<GridColumn>().Count()).IsEqualTo(2);

        var rows = table.Elements<TableRow>().ToList();
        await Assert.That(rows.Count).IsEqualTo(3);

        var headerCells = rows[0].Elements<TableCell>().ToList();
        await Assert.That(headerCells.Count).IsEqualTo(2);
        var headerParagraph = headerCells[0].GetFirstChild<Paragraph>()!;
        await Assert.That(headerParagraph.ParagraphProperties!.GetFirstChild<Justification>()!.Val?.Value)
            .IsEqualTo(JustificationValues.Center);
        var headerRun = headerParagraph.GetFirstChild<Run>()!;
        await Assert.That(headerRun.RunProperties!.GetFirstChild<Bold>()).IsNotNull();
        await Assert.That(headerRun.GetFirstChild<Text>()!.Text).IsEqualTo("A");

        var bodyCell = rows[1].Elements<TableCell>().First()
            .GetFirstChild<Paragraph>()!
            .GetFirstChild<Run>()!;
        await Assert.That(bodyCell.GetFirstChild<Text>()!.Text).IsEqualTo("1");
        await Assert.That(bodyCell.RunProperties?.GetFirstChild<Bold>()).IsNull();

        await VerifyDocument(md);
    }

    static async Task VerifyDocument(string markdown)
    {
        using var styleSource = DocxTemplateBuilder.Build();
        var store = new TemplateStore();
        store.RegisterMarkdownTemplate<EmptyModel>("table", markdown, styleSource);
        using var stream = new MemoryStream();
        await store.Render("table", new EmptyModel(), stream);
        stream.Position = 0;
        await Verify(stream, "docx");
    }
}
