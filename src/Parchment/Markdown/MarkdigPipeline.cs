namespace Parchment.Markdown;

internal static class MarkdigPipeline
{
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UseGridTables()
        .UsePipeTables()
        .UseAutoLinks()
        .UseListExtras()
        .UseSmartyPants()
        .UseGenericAttributes()
        .Build();
}
