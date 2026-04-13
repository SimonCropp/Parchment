namespace Parchment.Word;

/// <summary>
/// Builds the flat InnerText of a paragraph plus a map from character offset → (Text element, char index within).
/// Used to locate tokens that straddle run boundaries and apply substitutions back to the XML tree.
/// </summary>
internal sealed class ParagraphText
{
    readonly List<TextSpan> spans;
    readonly string innerText;

    ParagraphText(List<TextSpan> spans, string innerText)
    {
        this.spans = spans;
        this.innerText = innerText;
    }

    public string InnerText => innerText;
    public IReadOnlyList<TextSpan> Spans => spans;

    public static ParagraphText Build(Paragraph paragraph)
    {
        var spans = new List<TextSpan>();
        var builder = new StringBuilder();
        foreach (var text in paragraph.Descendants<Text>())
        {
            var offset = builder.Length;
            var value = text.Text;
            spans.Add(new(offset, value.Length, text));
            builder.Append(value);
        }

        return new(spans, builder.ToString());
    }

    /// <summary>
    /// Replaces a character range in the InnerText with the given replacement, preserving the formatting
    /// of the run that owns the first character of the range. Intermediate runs inside the range are deleted.
    /// </summary>
    public void Replace(int offset, int length, string replacement)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (offset < 0 || offset > innerText.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (length == 0)
        {
            return;
        }

        var end = offset + length;
        var first = FindSpan(offset, preferEnd: false);
        var last = FindSpan(end - 1, preferEnd: true);

        if (first.index == last.index)
        {
            ReplaceWithin(first, offset, length, replacement);
            return;
        }

        // Different spans — put all replacement text into the first span, and trim the rest.
        var firstSpan = spans[first.index];
        var localStart = offset - firstSpan.Offset;
        var firstText = firstSpan.Text.Text;
        var newFirstText = string.Concat(firstText.AsSpan(0, localStart), replacement);
        firstSpan.Text.Text = newFirstText;
        firstSpan.Text.Space = SpaceProcessingModeValues.Preserve;

        var lastSpan = spans[last.index];
        var localEnd = end - lastSpan.Offset;
        var lastText = lastSpan.Text.Text;
        lastSpan.Text.Text = lastText[localEnd..];
        if (lastSpan.Text.Text.Length > 0)
        {
            lastSpan.Text.Space = SpaceProcessingModeValues.Preserve;
        }

        // Remove any intermediate text elements entirely (empty them so downstream XML stays valid).
        for (var i = first.index + 1; i < last.index; i++)
        {
            spans[i].Text.Text = string.Empty;
        }
    }

    void ReplaceWithin(SpanRef reference, int offset, int length, string replacement)
    {
        var span = spans[reference.index];
        var local = offset - span.Offset;
        var source = span.Text.Text;
        var updated = string.Concat(source.AsSpan(0, local), replacement, source.AsSpan(local + length));
        span.Text.Text = updated;
        span.Text.Space = SpaceProcessingModeValues.Preserve;
    }

    SpanRef FindSpan(int absoluteOffset, bool preferEnd)
    {
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var end = span.Offset + span.Length;
            if (absoluteOffset >= span.Offset && absoluteOffset < end)
            {
                return new(i);
            }

            if (preferEnd && absoluteOffset == end && i == spans.Count - 1)
            {
                return new(i);
            }
        }

        throw new InvalidOperationException($"Offset {absoluteOffset} is not contained in any span (innerText length {innerText.Length}).");
    }

    readonly record struct SpanRef(int index);
}

internal sealed record TextSpan(int Offset, int Length, Text Text);
