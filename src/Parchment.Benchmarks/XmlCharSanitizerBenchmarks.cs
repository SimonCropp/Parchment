[Config(typeof(BenchmarkConfig))]
public class XmlCharSanitizerBenchmarks
{
    string shortAscii = null!;
    string shortUnicodeBmp = null!;
    string shortWithControl = null!;
    string longAscii = null!;
    string longWithControl = null!;
    string longWithSurrogatePair = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Realistic substitution-shaped values: numbers, names, dates, short text.
        shortAscii = "Service line item 42";
        // BMP unicode (no surrogates) — must hit fast path.
        shortUnicodeBmp = "café résumé 日本語";
        // Single control char in the middle — forces slow path.
        shortWithControl = "Serviceline item";

        // Larger payloads — exercise vectorized scan over ~1KB.
        longAscii = string.Join(' ', Enumerable.Range(1, 100).Select(_ => $"item-{_}"));
        longWithControl = longAscii.Insert(longAscii.Length / 2, "");
        // Valid surrogate pair somewhere in the string — fast-path IndexOfAny flags it,
        // slow path then validates as a paired surrogate and keeps it.
        longWithSurrogatePair = longAscii.Insert(longAscii.Length / 2, "😀");
    }

    [Benchmark]
    public string ShortAscii_FastPath() =>
        XmlCharSanitizer.Strip(shortAscii);

    [Benchmark]
    public string ShortUnicodeBmp_FastPath() =>
        XmlCharSanitizer.Strip(shortUnicodeBmp);

    [Benchmark]
    public string ShortWithControl_SlowPath() =>
        XmlCharSanitizer.Strip(shortWithControl);

    [Benchmark]
    public string LongAscii_FastPath() =>
        XmlCharSanitizer.Strip(longAscii);

    [Benchmark]
    public string LongWithControl_SlowPath() =>
        XmlCharSanitizer.Strip(longWithControl);

    [Benchmark]
    public string LongWithSurrogatePair_SlowPath() =>
        XmlCharSanitizer.Strip(longWithSurrogatePair);
}
