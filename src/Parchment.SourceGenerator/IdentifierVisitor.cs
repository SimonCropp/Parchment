sealed class IdentifierVisitor :
    AstVisitor
{
    List<string[]> paths = [];

    public static IReadOnlyList<string[]> Collect(IFluidTemplate template)
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
            paths.Add(segments.ToArray());
        }

        return base.VisitMemberExpression(memberExpression);
    }
}