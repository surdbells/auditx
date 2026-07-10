using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Identity.Events;

namespace AuditX.Domain.Identity;

/// <summary>
/// An AuditX user. Identity is sourced from the bank's Active Directory; AuditX stores no password.
/// A user authenticated for the first time is provisioned just-in-time with no roles
/// (<see cref="UserStatus.AwaitingRoleAssignment"/>) and cannot perform business actions until an
/// administrator grants at least one role.
/// </summary>
public sealed class User : AggregateRoot, ISoftDeletable
{
    private User()
    {
    }

    public string AdSamAccountName { get; private set; } = null!;

    public string AdUserPrincipalName { get; private set; } = null!;

    public string AdObjectSid { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    public string Timezone { get; private set; } = "UTC";

    public string Locale { get; private set; } = "en-GB";

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Free-form JSON of per-user notification preferences (US-M15-006).</summary>
    public string? NotificationPreferencesJson { get; private set; }

    /// <summary>The organisational unit the user belongs to (for utilisation/coverage reporting by org).</summary>
    public Guid? OrgUnitId { get; private set; }

    /// <summary>
    /// Annual audit capacity in person-days — the substrate for planned-load-vs-capacity workload reporting.
    /// Null when the user is not an auditor or has no declared capacity.
    /// </summary>
    public decimal? CapacityDays { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>Just-in-time provisioning from AD-asserted attributes (US-M1-005).</summary>
    public static User ProvisionFromDirectory(
        string adSamAccountName,
        string adUserPrincipalName,
        string adObjectSid,
        string email,
        string firstName,
        string lastName,
        string? displayName = null)
    {
        var user = new User
        {
            AdSamAccountName = Guard.NotNullOrWhiteSpace(adSamAccountName, "user.sam_required", "AD sAMAccountName is required."),
            AdUserPrincipalName = Guard.NotNullOrWhiteSpace(adUserPrincipalName, "user.upn_required", "AD userPrincipalName is required."),
            AdObjectSid = Guard.NotNullOrWhiteSpace(adObjectSid, "user.sid_required", "AD objectSid is required."),
            Email = Guard.NotNullOrWhiteSpace(email, "user.email_required", "Email is required."),
            FirstName = firstName?.Trim() ?? string.Empty,
            LastName = lastName?.Trim() ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{firstName} {lastName}".Trim() : displayName.Trim(),
            Status = UserStatus.AwaitingRoleAssignment,
        };

        user.RaiseDomainEvent(new UserProvisionedEvent(user.Id, user.AdSamAccountName, user.Email));
        return user;
    }

    /// <summary>Refresh AD-sourced profile attributes on a subsequent authentication.</summary>
    public void RefreshDirectoryAttributes(string email, string firstName, string lastName, string? displayName)
    {
        Email = Guard.NotNullOrWhiteSpace(email, "user.email_required", "Email is required.");
        FirstName = firstName?.Trim() ?? FirstName;
        LastName = lastName?.Trim() ?? LastName;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"{FirstName} {LastName}".Trim() : displayName.Trim();
    }

    public void RecordLogin(DateTimeOffset atUtc, AuthenticationMethod method)
    {
        if (Status == UserStatus.Deactivated)
        {
            throw new DomainException("user.deactivated", "Account is deactivated.");
        }

        LastLoginAt = atUtc;
        RaiseDomainEvent(new UserLoggedInEvent(Id, method));
    }

    /// <summary>
    /// Promote an awaiting-role user to active once they hold at least one role. Idempotent for
    /// users already active.
    /// </summary>
    public void MarkActiveOnFirstRole()
    {
        if (Status == UserStatus.AwaitingRoleAssignment)
        {
            Status = UserStatus.Active;
        }
    }

    public void Deactivate(Guid? by)
    {
        if (Status == UserStatus.Deactivated)
        {
            return;
        }

        Status = UserStatus.Deactivated;
        RaiseDomainEvent(new UserDeactivatedEvent(Id, by));
    }

    public void Reactivate()
    {
        if (Status == UserStatus.Deactivated)
        {
            Status = UserStatus.AwaitingRoleAssignment;
        }
    }

    public void UpdateNotificationPreferences(string? preferencesJson) => NotificationPreferencesJson = preferencesJson;

    /// <summary>Assigns (or clears) the organisational unit the user belongs to.</summary>
    public void SetOrgUnit(Guid? orgUnitId) => OrgUnitId = orgUnitId;

    /// <summary>Set (or clear) the user's annual audit capacity in person-days. Bounded to a single year.</summary>
    public void SetCapacityDays(decimal? days)
    {
        if (days is { } d)
        {
            if (d < 0m)
            {
                throw new DomainException("user.capacity_negative", "Capacity days cannot be negative.");
            }

            if (d > 366m)
            {
                throw new DomainException("user.capacity_too_large", "Capacity days cannot exceed 366 (one year).");
            }
        }

        CapacityDays = days;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }
}
