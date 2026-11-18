namespace AuditX.Domain.Enums;

/// <summary>
/// An engagement letter is written as a <see cref="Draft"/>, <see cref="Issued"/> to the auditee, and
/// <see cref="Acknowledged"/> by them. Changing an issued letter makes it a draft of the next revision again.
/// </summary>
public enum EngagementLetterStatus
{
    Draft,
    Issued,
    Acknowledged,
}

/// <summary>The two meetings an engagement records: the kickoff before fieldwork and the exit on the draft report.</summary>
public enum MeetingKind
{
    Kickoff,
    Exit,
}
