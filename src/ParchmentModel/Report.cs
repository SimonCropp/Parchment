namespace ParchmentModel;

#region ReportModel
public class ReportContext
{
    public required Report Report { get; init; }
}

public class Report
{
    public required string Title { get; init; }
    public required string Author { get; init; }
    public required Date Date { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<Finding> Findings { get; init; }
    public required IReadOnlyList<ActionItem> Actions { get; init; }
    public required bool HasRisks { get; init; }
}

public class Finding
{
    public required string Area { get; init; }
    public required string Status { get; init; }
    public required string Owner { get; init; }
}

public class ActionItem
{
    public required string Title { get; init; }
    public required string Detail { get; init; }
}
#endregion
