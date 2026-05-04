/// <summary>
/// One <c>WordNumberingState</c> is allocated per render and shared across every code path that
/// can introduce list numbering — string-list auto-bullets, [Markdown] content, loop bodies, and
/// if-branches. These tests assert that invariant on every path: a render that produces N
/// equivalent lists must produce exactly one bullet (or ordered) abstract definition shared by N
/// numbering instances.
///
/// Before the shared-state fix, each token built its own <c>WordNumberingState</c>, allocating
/// duplicate abstracts whose final IDs depended on OOXML SDK save-time reordering — making rendered
/// docx bytes non-deterministic across runs.
/// </summary>
public class NumberingStateSharingTests
{
    [AttributeUsage(AttributeTargets.Property)]
    sealed class MarkdownAttribute : Attribute;

    public class TwoMarkdownDoc
    {
        [Markdown]
        public required string FirstBody { get; init; }

        [Markdown]
        public required string SecondBody { get; init; }
    }

    public class MixedDoc
    {
        public required IReadOnlyList<string> Tags { get; init; }

        [Markdown]
        public required string Body { get; init; }
    }

    public class GroupsDoc
    {
        public required IReadOnlyList<Group> Groups { get; init; }
        public required IReadOnlyList<string> Outer { get; init; }
    }

    public class Group
    {
        public required IReadOnlyList<string> Items { get; init; }
    }

    public class IfDoc
    {
        public required bool ShowInner { get; init; }
        public required IReadOnlyList<string> Inner { get; init; }
        public required IReadOnlyList<string> Outer { get; init; }
    }

    [Test]
    public async Task TwoMarkdownPropertiesShareOneBulletAbstract()
    {
        // Two [Markdown] properties each containing a bullet list go through
        // OpenXmlMarkdownRenderer; both must reuse the shared WordNumberingState's bullet abstract
        // rather than each constructing a fresh state.
        using var template = DocxTemplateBuilder.Build(
            """
            {{ FirstBody }}

            {{ SecondBody }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<TwoMarkdownDoc>("two-md", template);

        var model = new TwoMarkdownDoc
        {
            FirstBody = "- a1\n- a2",
            SecondBody = "- b1\n- b2"
        };

        await AssertOneSharedBulletAbstract(store, "two-md", model, expectedInstanceCount: 2);
    }

    [Test]
    public async Task StringListAndMarkdownShareOneBulletAbstract()
    {
        // Mixed paths: an IEnumerable<string> auto-bullet (TokenValueHelpers.BulletList) and a
        // [Markdown] bullet list (OpenXmlMarkdownRenderer) must share the same bullet abstract.
        using var template = DocxTemplateBuilder.Build(
            """
            {{ Tags }}

            {{ Body }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<MixedDoc>("mixed", template);

        var model = new MixedDoc
        {
            Tags = ["t1", "t2"],
            Body = "- m1\n- m2"
        };

        await AssertOneSharedBulletAbstract(store, "mixed", model, expectedInstanceCount: 2);
    }

    [Test]
    public async Task LoopAndOuterListsShareOneBulletAbstract()
    {
        // The cloned ScopeTreeRunner inside a {% for %} loop body must inherit the parent runner's
        // WordNumberingState. The loop body uses the explicit `bullet_list` filter (auto-bullet
        // rendering skips loop scope), and combined with the outer auto-bullet list there should be
        // 3 NumberingInstances (2 from the loop iterations + 1 outer) all pointing at the same
        // shared bullet abstract.
        using var template = DocxTemplateBuilder.Build(
            """
            {% for group in Groups %}

            {{ group.Items | bullet_list }}

            {% endfor %}

            {{ Outer }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<GroupsDoc>("loop-share", template);

        var model = new GroupsDoc
        {
            Groups =
            [
                new() { Items = ["g1a", "g1b"] },
                new() { Items = ["g2a", "g2b"] }
            ],
            Outer = ["o1", "o2"]
        };

        await AssertOneSharedBulletAbstract(store, "loop-share", model, expectedInstanceCount: 3);
    }

    [Test]
    public async Task IfBranchAndOuterListsShareOneBulletAbstract()
    {
        // The inner ScopeTreeRunner spawned for {% if %} branch evaluation must inherit the parent
        // runner's WordNumberingState. One inside-branch list + one outside list = 2 bullet
        // NumberingInstances all pointing at the same bullet abstract.
        using var template = DocxTemplateBuilder.Build(
            """
            {% if ShowInner %}

            {{ Inner }}

            {% endif %}

            {{ Outer }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<IfDoc>("if-share", template);

        var model = new IfDoc
        {
            ShowInner = true,
            Inner = ["i1", "i2"],
            Outer = ["o1", "o2"]
        };

        await AssertOneSharedBulletAbstract(store, "if-share", model, expectedInstanceCount: 2);
    }

    [Test]
    public async Task TwoMarkdownOrderedListsShareOneOrderedAbstract()
    {
        // The shared-state guarantee covers ordered lists too — WordNumberingState caches one
        // abstract per NumberFormatValues, so two markdown-rendered ordered lists must share.
        using var template = DocxTemplateBuilder.Build(
            """
            {{ FirstBody }}

            {{ SecondBody }}
            """);

        var store = new TemplateStore();
        store.RegisterDocxTemplate<TwoMarkdownDoc>("two-md-ordered", template);

        var model = new TwoMarkdownDoc
        {
            FirstBody = "1. a1\n2. a2",
            SecondBody = "1. b1\n2. b2"
        };

        using var stream = new MemoryStream();
        await store.Render("two-md-ordered", model, stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var numbering = doc.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        var orderedDecimalAbstracts = numbering
            .Elements<AbstractNum>()
            .Where(a =>
                a.Elements<Level>().FirstOrDefault(_ => _.LevelIndex?.Value == 0) is { } lvl &&
                lvl.NumberingFormat?.Val?.Value == NumberFormatValues.Decimal)
            .ToList();
        var instancesPointingAtOrdered = numbering
            .Elements<NumberingInstance>()
            .Where(i =>
                i.AbstractNumId?.Val?.Value is { } refId &&
                orderedDecimalAbstracts.Any(a => a.AbstractNumberId?.Value == refId))
            .ToList();

        await Assert.That(orderedDecimalAbstracts.Count).IsEqualTo(1);
        await Assert.That(instancesPointingAtOrdered.Count).IsEqualTo(2);
    }

    static async Task AssertOneSharedBulletAbstract(
        TemplateStore store,
        string name,
        object model,
        int expectedInstanceCount)
    {
        using var stream = new MemoryStream();
        await store.Render(name, model, stream);
        stream.Position = 0;

        using var doc = WordprocessingDocument.Open(stream, false);
        var numbering = doc.MainDocumentPart!.NumberingDefinitionsPart!.Numbering!;
        var bulletAbstracts = numbering
            .Elements<AbstractNum>()
            .Where(a =>
                a.Elements<Level>().FirstOrDefault(_ => _.LevelIndex?.Value == 0) is { } lvl &&
                lvl.NumberingFormat?.Val?.Value == NumberFormatValues.Bullet &&
                lvl.LevelText?.Val?.Value == "●")
            .ToList();
        var instancesPointingAtBullet = numbering
            .Elements<NumberingInstance>()
            .Where(i =>
                i.AbstractNumId?.Val?.Value is { } refId &&
                bulletAbstracts.Any(a => a.AbstractNumberId?.Value == refId))
            .ToList();

        await Assert.That(bulletAbstracts.Count).IsEqualTo(1);
        await Assert.That(instancesPointingAtBullet.Count).IsEqualTo(expectedInstanceCount);
    }
}
