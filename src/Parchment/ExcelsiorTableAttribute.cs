namespace Parchment;

/// <summary>
/// Marks a model property whose value should be rendered as a Word table by
/// <c>Excelsior.WordTableBuilder</c> when referenced via a <c>{{ Property }}</c> substitution
/// token. The property must be an <see cref="System.Collections.Generic.IEnumerable{T}"/>; element
/// columns, headings, ordering, and formatting are then derived from the element type's
/// <c>[Column]</c> attributes per Excelsior's normal conventions.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ExcelsiorTableAttribute :
    Attribute;
