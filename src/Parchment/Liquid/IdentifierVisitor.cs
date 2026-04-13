namespace Parchment.Liquid;

/// <summary>
/// Walks a Fluid AST collecting the root identifiers of every member expression it sees.
/// Used by ModelValidator to check that referenced model members actually exist.
/// </summary>
internal sealed class IdentifierVisitor :
    AstVisitor
{
    readonly List<IdentifierPath> paths = [];

    public IReadOnlyList<IdentifierPath> Paths => paths;

    public static IReadOnlyList<IdentifierPath> Collect(IFluidTemplate template)
    {
        var visitor = new IdentifierVisitor();
        visitor.VisitTemplate(template);
        return visitor.paths;
    }

    protected override Expression VisitMemberExpression(MemberExpression memberExpression)
    {
        var segments = new List<string>(memberExpression.Segments.Count);
        foreach (var segment in memberExpression.Segments)
        {
            if (segment is IdentifierSegment identifier)
            {
                segments.Add(identifier.Identifier);
            }
            else
            {
                break;
            }
        }

        if (segments.Count > 0)
        {
            paths.Add(new(segments));
        }

        return base.VisitMemberExpression(memberExpression);
    }
}

internal sealed record IdentifierPath(IReadOnlyList<string> Segments)
{
    public string Root => Segments[0];

    public override string ToString() =>
        string.Join('.', Segments);
}
