sealed class IdentifierVisitor :
    AstVisitor
{
    List<List<string>> paths = [];

    public static List<List<string>> Collect(IFluidTemplate template)
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
            paths.Add(segments);
        }

        return base.VisitMemberExpression(memberExpression);
    }
}
