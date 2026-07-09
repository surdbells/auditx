namespace AuditX.Domain.Enums;

/// <summary>
/// The kind of response a checklist item expects. Verdict types (<see cref="PassFailNa"/>, <see cref="YesNo"/>)
/// capture a Pass/Fail/N-A conclusion directly. Value types (<see cref="Text"/>, <see cref="Numeric"/>,
/// <see cref="Date"/>, <see cref="Rating"/>, <see cref="MultipleChoice"/>) capture a typed value; a verdict
/// remains optional so any item can still drive the exception workflow.
/// </summary>
public enum ResponseType
{
    PassFailNa,
    YesNo,
    Text,
    Numeric,
    Date,
    Rating,
    MultipleChoice,
}

/// <summary>Helpers for reasoning about a <see cref="ResponseType"/>.</summary>
public static class ResponseTypes
{
    /// <summary>Value types capture a typed value (text/number/date/rating/choice) rather than only a verdict.</summary>
    public static bool IsValueType(this ResponseType type) => type is
        ResponseType.Text or ResponseType.Numeric or ResponseType.Date or ResponseType.Rating or ResponseType.MultipleChoice;
}
