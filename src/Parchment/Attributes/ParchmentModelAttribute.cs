namespace Parchment;

/// <summary>
/// Declares that the decorated <c>partial</c> class is the binding model for a Parchment
/// template, validated at compile time by the Parchment source generator. The template path is
/// resolved against <c>AdditionalFiles</c>. The attribute is applied directly to the model class
/// — there is no separate marker / "template" class. See CLAUDE.md → "Design decisions".
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ParchmentModelAttribute(string templatePath) :
    Attribute
{
    public string TemplatePath { get; } = templatePath;
}
