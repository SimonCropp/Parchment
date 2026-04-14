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