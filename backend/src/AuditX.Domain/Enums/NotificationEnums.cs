namespace AuditX.Domain.Enums;

/// <summary>Delivery channel for a notification (M10). In-app is deferred to a later release.</summary>
public enum NotificationChannel
{
    Email,
    Sms,

    /// <summary>Posts to a Microsoft Teams channel via an incoming webhook. Channel-scoped (one webhook = one
    /// team channel), so it is delivered once per (event, rule) rather than once per recipient.</summary>
    Teams,
}

/// <summary>Lifecycle of a single notification dispatch (M10).</summary>
public enum DispatchStatus
{
    Pending,
    Dispatched,
    Delivered,
    Bounced,
    Failed,
    DeadLetter,
}

/// <summary>How a notification rule resolves its recipients (M10).</summary>
public enum RecipientResolutionType
{
    /// <summary>All active users holding a named role (value = role name).</summary>
    Role,

    /// <summary>An explicit comma-separated list of user ids (value = "guid,guid").</summary>
    NamedUsers,

    /// <summary>A user-id field read from the event payload (value = field name, e.g. "OwnerUserId").</summary>
    PayloadDerived,

    /// <summary>The line manager of a payload user (value = the payload field holding the subject's user id).</summary>
    LineManager,

    /// <summary>Every manager up the reporting chain above a payload user (value = that payload field).</summary>
    ReportingChain,
}

/// <summary>A template is either a built-in system default or a bank-specific override.</summary>
public enum TemplateScope
{
    System,
    Bank,
}
