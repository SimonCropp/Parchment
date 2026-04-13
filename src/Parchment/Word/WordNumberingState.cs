namespace Parchment.Word;

/// <summary>
/// Adapter for <see cref="NumberingDefinitionsPart"/> that reuses the existing definitions when present
/// and creates new ones on demand for bullet and ordered lists.
/// </summary>
internal sealed class WordNumberingState(MainDocumentPart mainPart)
{
    readonly MainDocumentPart mainPart = mainPart;
    int? bulletAbstractNumId;
    readonly Dictionary<NumberFormatValues, int> orderedAbstractNumIds = [];
    int nextAbstractNumId;
    int nextNumId;
    bool initialized;

    public int CreateBulletNumbering()
    {
        EnsureInitialized();
        var numbering = GetNumbering();
        var abstractId = bulletAbstractNumId ?? CreateBulletAbstract(numbering);
        bulletAbstractNumId = abstractId;
        return AppendInstance(numbering, abstractId);
    }

    public int CreateOrderedNumbering(NumberFormatValues format)
    {
        EnsureInitialized();
        var numbering = GetNumbering();
        if (!orderedAbstractNumIds.TryGetValue(format, out var abstractId))
        {
            abstractId = CreateOrderedAbstract(numbering, format);
            orderedAbstractNumIds[format] = abstractId;
        }

        return AppendInstance(numbering, abstractId);
    }

    Numbering GetNumbering()
    {
        var part = mainPart.NumberingDefinitionsPart ?? mainPart.AddNewPart<NumberingDefinitionsPart>();
        if (part.Numbering == null)
        {
            part.Numbering = new();
            part.Numbering.Save();
        }

        return part.Numbering;
    }

    void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        var part = mainPart.NumberingDefinitionsPart;
        if (part?.Numbering == null)
        {
            nextAbstractNumId = 1;
            nextNumId = 1;
            return;
        }

        foreach (var abstractNum in part.Numbering.Elements<AbstractNum>())
        {
            var idStr = abstractNum.AbstractNumberId?.Value;
            if (idStr.HasValue && idStr.Value >= nextAbstractNumId)
            {
                nextAbstractNumId = idStr.Value + 1;
            }
        }

        foreach (var numberingInstance in part.Numbering.Elements<NumberingInstance>())
        {
            var idStr = numberingInstance.NumberID?.Value;
            if (idStr.HasValue && idStr.Value >= nextNumId)
            {
                nextNumId = idStr.Value + 1;
            }
        }

        if (nextAbstractNumId == 0)
        {
            nextAbstractNumId = 1;
        }

        if (nextNumId == 0)
        {
            nextNumId = 1;
        }
    }

    int CreateBulletAbstract(Numbering numbering)
    {
        var id = nextAbstractNumId++;
        var abstractNum = new AbstractNum { AbstractNumberId = id };
        abstractNum.Append(
            BuildBulletLevel(0, "\u25CF"),
            BuildBulletLevel(1, "\u25CB"),
            BuildBulletLevel(2, "\u25A0"));
        numbering.InsertAt(abstractNum, 0);
        return id;
    }

    int CreateOrderedAbstract(Numbering numbering, NumberFormatValues format)
    {
        var id = nextAbstractNumId++;
        var abstractNum = new AbstractNum { AbstractNumberId = id };
        abstractNum.Append(
            BuildOrderedLevel(0, format),
            BuildOrderedLevel(1, format),
            BuildOrderedLevel(2, format));
        numbering.InsertAt(abstractNum, 0);
        return id;
    }

    int AppendInstance(Numbering numbering, int abstractId)
    {
        var numId = nextNumId++;
        var instance = new NumberingInstance
        {
            NumberID = numId
        };
        instance.Append(new AbstractNumId { Val = abstractId });
        numbering.Append(instance);
        return numId;
    }

    static Level BuildBulletLevel(int ilvl, string glyph) =>
        new()
        {
            LevelIndex = ilvl,
            NumberingFormat = new() { Val = NumberFormatValues.Bullet },
            LevelText = new() { Val = glyph },
            LevelJustification = new() { Val = LevelJustificationValues.Left },
            PreviousParagraphProperties = new(
                new Indentation
                {
                    Left = (720 + 360 * ilvl).ToString(),
                    Hanging = "360"
                }),
            NumberingSymbolRunProperties = new(
                new RunFonts
                {
                    Ascii = "Symbol",
                    HighAnsi = "Symbol",
                    Hint = FontTypeHintValues.Default
                })
        };

    static Level BuildOrderedLevel(int ilvl, NumberFormatValues format) =>
        new()
        {
            LevelIndex = ilvl,
            StartNumberingValue = new() { Val = 1 },
            NumberingFormat = new() { Val = format },
            LevelText = new() { Val = $"%{ilvl + 1}." },
            LevelJustification = new() { Val = LevelJustificationValues.Left },
            PreviousParagraphProperties = new(
                new Indentation
                {
                    Left = (720 + 360 * ilvl).ToString(),
                    Hanging = "360"
                })
        };
}
