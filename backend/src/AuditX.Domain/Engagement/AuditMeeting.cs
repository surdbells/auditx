using AuditX.Domain.Common;
using AuditX.Domain.Engagement.Events;
using AuditX.Domain.Enums;

namespace AuditX.Domain.Engagement;

/// <summary>
/// The record of one of an engagement's two meetings with management: the kickoff before fieldwork and the exit
/// meeting held on the draft report. Who attended (people on the platform, and others by name), the minutes, and the
/// points agreed. The exit meeting names the draft report it discussed. One record of each kind per audit; it can be
/// corrected, and the audit trail keeps every version. A standalone aggregate, versioned on its own.
/// </summary>
public sealed class AuditMeeting : AggregateRoot
{
    public const int MaxMinutesLength = 8000;
    public const int MaxPointLength = 1000;
    public const int MaxPoints = 50;
    public const int MaxAttendees = 100;
    public const int MaxExternalAttendeeLength = 200;

    private AuditMeeting()
    {
    }

    public Guid AuditId { get; private set; }

    public MeetingKind Kind { get; private set; }

    public DateTimeOffset HeldAt { get; private set; }

    /// <summary>Attendees who are users of the platform.</summary>
    public List<Guid> AttendeeUserIds { get; private set; } = [];

    /// <summary>Attendees who are not, by name (and role, as written).</summary>
    public List<string> ExternalAttendees { get; private set; } = [];

    public string Minutes { get; private set; } = null!;

    /// <summary>What was agreed, one point each (actions, dates, the factual accuracy of findings).</summary>
    public List<string> AgreedPoints { get; private set; } = [];

    /// <summary>The draft report the exit meeting discussed; null for a kickoff.</summary>
    public Guid? ReportId { get; private set; }

    public Guid RecordedBy { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? LastEditedBy { get; private set; }

    public DateTimeOffset? LastEditedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static AuditMeeting Record(
        Guid auditId, MeetingKind kind, DateTimeOffset heldAt, IEnumerable<Guid> attendeeUserIds, IEnumerable<string> externalAttendees,
        string minutes, IEnumerable<string> agreedPoints, Guid? reportId, Guid recordedBy, DateTimeOffset nowUtc,
        string auditName, Guid auditeeUserId)
    {
        Guard.Against(auditId == Guid.Empty, "meeting.audit_required", "An audit is required.");
        var meeting = new AuditMeeting { AuditId = auditId, Kind = kind, RecordedBy = recordedBy, RecordedAt = nowUtc };
        meeting.Apply(heldAt, attendeeUserIds, externalAttendees, minutes, agreedPoints, reportId, nowUtc);
        meeting.RaiseDomainEvent(new AuditMeetingRecordedEvent(meeting.Id, auditId, auditName, Snake(kind), auditeeUserId, IsCorrection: false));
        return meeting;
    }

    /// <summary>Correct the record (a missed attendee, a point worded more precisely).</summary>
    public void Correct(
        DateTimeOffset heldAt, IEnumerable<Guid> attendeeUserIds, IEnumerable<string> externalAttendees, string minutes,
        IEnumerable<string> agreedPoints, Guid? reportId, Guid editedBy, DateTimeOffset nowUtc, string auditName, Guid auditeeUserId)
    {
        Apply(heldAt, attendeeUserIds, externalAttendees, minutes, agreedPoints, reportId, nowUtc);
        LastEditedBy = editedBy;
        LastEditedAt = nowUtc;
        RaiseDomainEvent(new AuditMeetingRecordedEvent(Id, AuditId, auditName, Snake(Kind), auditeeUserId, IsCorrection: true));
    }

    private void Apply(
        DateTimeOffset heldAt, IEnumerable<Guid> attendeeUserIds, IEnumerable<string> externalAttendees, string minutes,
        IEnumerable<string> agreedPoints, Guid? reportId, DateTimeOffset nowUtc)
    {
        // A few minutes' allowance for clock differences between the recorder's device and the server.
        Guard.Against(heldAt > nowUtc.AddMinutes(5), "meeting.held_in_future", "A meeting is recorded once it has been held.");

        var users = attendeeUserIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var others = externalAttendees
            .Select(a => a?.Trim() ?? string.Empty)
            .Where(a => a.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Guard.Against(users.Count + others.Count == 0, "meeting.attendees_required", "Record who attended the meeting.");
        Guard.Against(users.Count + others.Count > MaxAttendees, "meeting.too_many_attendees", $"A meeting can list at most {MaxAttendees} attendees.");
        Guard.Against(others.Any(a => a.Length > MaxExternalAttendeeLength), "meeting.attendee_too_long",
            $"Keep each attendee's name and role to {MaxExternalAttendeeLength} characters or fewer.");

        var trimmedMinutes = Guard.NotNullOrWhiteSpace(minutes, "meeting.minutes_required", "Write the minutes of the meeting.").Trim();
        Guard.Against(trimmedMinutes.Length > MaxMinutesLength, "meeting.minutes_too_long", $"Keep the minutes to {MaxMinutesLength} characters or fewer.");

        var points = agreedPoints.Select(p => p?.Trim() ?? string.Empty).Where(p => p.Length > 0).ToList();
        Guard.Against(points.Count > MaxPoints, "meeting.too_many_points", $"Record at most {MaxPoints} agreed points.");
        Guard.Against(points.Any(p => p.Length > MaxPointLength), "meeting.point_too_long", $"Keep each agreed point to {MaxPointLength} characters or fewer.");

        if (Kind == MeetingKind.Exit)
        {
            Guard.Against(reportId is null || reportId == Guid.Empty, "meeting.draft_report_required", "Choose the draft report the exit meeting discussed.");
        }
        else
        {
            Guard.Against(reportId is not null, "meeting.report_not_applicable", "Only the exit meeting discusses a draft report.");
        }

        HeldAt = heldAt;
        AttendeeUserIds = users;
        ExternalAttendees = others;
        Minutes = trimmedMinutes;
        AgreedPoints = points;
        ReportId = reportId;
    }

    private static string Snake(MeetingKind kind) => kind == MeetingKind.Kickoff ? "kickoff" : "exit";
}
