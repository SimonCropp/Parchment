public class ParchmentTemplateGeneratorTests
{
    const string letterModel = """
                               using Parchment;

                               namespace Sample;

                               public class Customer
                               {
                                   public string Name { get; set; } = "";
                               }

                               public class Letter
                               {
                                   public Customer Customer { get; set; } = new();
                               }
                               """;

    [Test]
    public Task Substitution_Valid()
    {
        var source = letterModel +
                     """

                     [ParchmentTemplate("template.docx", typeof(Letter))]
                     public partial class CustomerLetter;
                     """;
        var result = GeneratorDriver.Run(source, "Hello {{ Customer.Name }}!");
        return Verify(result);
    }

    [Test]
    public Task Substitution_MissingMember()
    {
        var source = letterModel +
                     """

                     [ParchmentTemplate("template.docx", typeof(Letter))]
                     public partial class CustomerLetter;
                     """;
        var result = GeneratorDriver.Run(source, "{{ Customer.Missing }}");
        return Verify(result);
    }

    [Test]
    public Task ForLoop_Valid()
    {
        var source = """
                     using System.Collections.Generic;
                     using Parchment;

                     namespace Sample;

                     public class Line
                     {
                         public string Description { get; set; } = "";
                     }

                     public class Invoice
                     {
                         public List<Line> Lines { get; set; } = new();
                     }

                     [ParchmentTemplate("template.docx", typeof(Invoice))]
                     public partial class InvoiceDoc;
                     """;
        var result = GeneratorDriver.Run(
            source,
            "{% for line in Lines %}",
            "{{ line.Description }}",
            "{% endfor %}");
        return Verify(result);
    }

    [Test]
    public Task ForLoop_SourceNotEnumerable()
    {
        var source = letterModel +
                     """

                     [ParchmentTemplate("template.docx", typeof(Letter))]
                     public partial class BadLoop;
                     """;
        var result = GeneratorDriver.Run(
            source,
            "{% for item in Customer %}",
            "x",
            "{% endfor %}");
        return Verify(result);
    }

    [Test]
    public Task UnknownBlockTag()
    {
        var source = """
                     using Parchment;

                     namespace Sample;

                     public class Empty;

                     [ParchmentTemplate("template.docx", typeof(Empty))]
                     public partial class WeirdTag;
                     """;
        var result = GeneratorDriver.Run(source, "{% foobar %}");
        return Verify(result);
    }

    [Test]
    public Task TemplateFileMissing()
    {
        var source = """
                     using Parchment;

                     namespace Sample;

                     public class Empty;

                     [ParchmentTemplate("does-not-exist.docx", typeof(Empty))]
                     public partial class MissingFile;
                     """;
        var result = GeneratorDriver.Run(source, "ignored");
        return Verify(result);
    }

    [Test]
    public Task ForLoop_StringIsNotEnumerable()
    {
        // Guards against string being silently treated as IEnumerable<char>. ShapeBuilder
        // has a special-case for SpecialType.System_String; this test fails loudly if that
        // guard is removed (loop would validate as legal "for c in <string>" instead).
        var source = letterModel +
                     """

                     [ParchmentTemplate("template.docx", typeof(Letter))]
                     public partial class StringLoop;
                     """;
        var result = GeneratorDriver.Run(
            source,
            "{% for c in Customer.Name %}",
            "x",
            "{% endfor %}");
        return Verify(result);
    }

    [Test]
    public Task MultiTarget_MultiDocx()
    {
        var source = """
                     using Parchment;

                     namespace Sample;

                     public class Customer
                     {
                         public string Name { get; set; } = "";
                     }

                     public class Letter
                     {
                         public Customer Customer { get; set; } = new();
                     }

                     public class Invoice
                     {
                         public decimal Total { get; set; }
                     }

                     [ParchmentTemplate("letter.docx", typeof(Letter))]
                     public partial class LetterDoc;

                     [ParchmentTemplate("invoice.docx", typeof(Invoice))]
                     public partial class InvoiceDoc;
                     """;

        var setup = GeneratorDriver.CreateDriverWithDocxes(
            source,
            ("letter.docx", GeneratorDriver.BuildDocxBytes("Hello {{ Customer.Name }}!")),
            ("invoice.docx", GeneratorDriver.BuildDocxBytes("Total: {{ Total }}")));

        var result = setup.Driver.RunGenerators(setup.Compilation).GetRunResult();
        return Verify(result);
    }

    [Test]
    public async Task TemplateReadError_CorruptDocx()
    {
        // PARCH006: exception message is platform/culture dependent, so assert the diagnostic
        // id directly instead of snapshotting the full message.
        var source = """
                     using Parchment;

                     namespace Sample;

                     public class Empty;

                     [ParchmentTemplate("template.docx", typeof(Empty))]
                     public partial class Corrupt;
                     """;

        var setup = GeneratorDriver.CreateDriverWithDocxes(
            source,
            ("template.docx", "not a zip file"u8.ToArray()));

        var result = setup.Driver.RunGenerators(setup.Compilation).GetRunResult();
        var diagnostics = result.Results.Single().Diagnostics;

        await Assert.That(diagnostics.Length).IsEqualTo(1);
        await Assert.That(diagnostics[0].Id).IsEqualTo("PARCH006");
    }

    [Test]
    public Task MixedInlineBlockTag()
    {
        var source = """
                     using System.Collections.Generic;
                     using Parchment;

                     namespace Sample;

                     public class Line
                     {
                         public string Description { get; set; } = "";
                     }

                     public class Invoice
                     {
                         public List<Line> Lines { get; set; } = new();
                     }

                     [ParchmentTemplate("template.docx", typeof(Invoice))]
                     public partial class Mixed;
                     """;
        var result = GeneratorDriver.Run(
            source,
            "prefix {% for line in Lines %}",
            "{{ line.Description }}",
            "{% endfor %}");
        return Verify(result);
    }
}