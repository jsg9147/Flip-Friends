using System.Collections.Generic;
using System.Linq;

public sealed class MapValidationReport
{
    public IReadOnlyList<MapValidationIssue> Issues { get; }
    public bool HasErrors => Issues.Any(issue => issue.Severity == MapValidationSeverity.Error);
    public bool HasWarnings => Issues.Any(issue => issue.Severity == MapValidationSeverity.Warning);
    public bool CanSave => true;
    public bool CanStartPlay => !HasErrors;

    public MapValidationReport(IReadOnlyList<MapValidationIssue> issues)
    {
        Issues = issues;
    }
}
