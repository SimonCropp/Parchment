namespace Parchment.Markdown.Renderers;

class TableRenderer :
    MarkdownObjectRenderer<OpenXmlMarkdownRenderer, Markdig.Extensions.Tables.Table>
{
    protected override void Write(OpenXmlMarkdownRenderer renderer, Markdig.Extensions.Tables.Table tableBlock)
    {
        var table = new Table();
        table.Append(BuildTableProperties());
        table.Append(BuildTableGrid(tableBlock));

        foreach (var child in tableBlock)
        {
            if (child is Markdig.Extensions.Tables.TableRow row)
            {
                table.Append(BuildRow(renderer, row));
            }
        }

        renderer.AddBlock(table);
    }

    static TableProperties BuildTableProperties() =>
        new(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }));

    static TableGrid BuildTableGrid(Markdig.Extensions.Tables.Table table)
    {
        var grid = new TableGrid();
        var columns = table.ColumnDefinitions.Count;
        for (var i = 0; i < columns; i++)
        {
            grid.Append(new GridColumn());
        }

        return grid;
    }

    static TableRow BuildRow(OpenXmlMarkdownRenderer renderer, Markdig.Extensions.Tables.TableRow row)
    {
        var tableRow = new TableRow();
        foreach (var cell in row.OfType<Markdig.Extensions.Tables.TableCell>())
        {
            tableRow.Append(BuildCell(renderer, cell, row.IsHeader));
        }

        return tableRow;
    }

    static TableCell BuildCell(OpenXmlMarkdownRenderer renderer, Markdig.Extensions.Tables.TableCell cell, bool isHeader)
    {
        var tableCell = new TableCell();
        renderer.PushContainer();
        foreach (var child in cell)
        {
            renderer.Render(child);
        }

        var state = renderer.PopContainer();
        if (state.CurrentRuns.Count > 0)
        {
            var paragraph = new Paragraph();
            foreach (var run in state.CurrentRuns)
            {
                paragraph.Append(run);
            }

            state.Blocks.Add(paragraph);
        }

        if (state.Blocks.Count == 0)
        {
            state.Blocks.Add(new Paragraph());
        }

        foreach (var block in state.Blocks)
        {
            if (block is Paragraph p && isHeader)
            {
                p.ParagraphProperties ??= new();
                p.ParagraphProperties.Append(new Justification { Val = JustificationValues.Center });
                foreach (var run in p.Elements<Run>())
                {
                    run.RunProperties ??= new();
                    run.RunProperties.Append(new Bold());
                }
            }

            tableCell.Append(block);
        }

        return tableCell;
    }
}
