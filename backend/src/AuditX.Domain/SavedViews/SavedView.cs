using AuditX.Domain.Common;

namespace AuditX.Domain.SavedViews;

/// <summary>
/// A named, reusable filter / parameter set for a list or analytics screen (D3-A). Owner-scoped: a user saves the
/// current filter selection under a name and re-applies it later; an owner may mark a view <see cref="IsShared"/> so
/// the whole function can apply it (read-only to non-owners). <see cref="ViewKey"/> scopes a view to one screen (e.g.
/// <c>exceptions</c>) so each screen only offers its own views. <see cref="ParametersJson"/> is an opaque JSON payload
/// the frontend interprets — applying a view only pre-fills filter controls; the underlying data is still permission-
/// gated by its own endpoint, so a shared view can never widen access. A standalone soft-deletable, rowversion-guarded
/// aggregate.
/// </summary>
public sealed class SavedView : Entity, ISoftDeletable
{
    private SavedView()
    {
    }

    /// <summary>The user who created and owns the view; only the owner may edit, share or delete it.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Identifies the screen the view applies to (e.g. <c>exceptions</c>, <c>audits</c>).</summary>
    public string ViewKey { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    /// <summary>Opaque JSON filter/parameter payload the frontend interprets for this screen.</summary>
    public string ParametersJson { get; private set; } = null!;

    /// <summary>When true, every user may see and apply the view (read-only); otherwise it is private to the owner.</summary>
    public bool IsShared { get; private set; }

    public byte[] Version { get; private set; } = [];

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static SavedView Create(Guid ownerUserId, string viewKey, string name, string parametersJson, bool isShared)
    {
        Guard.Against(ownerUserId == Guid.Empty, "saved_view.owner_required", "An owner is required.");
        return new SavedView
        {
            OwnerUserId = ownerUserId,
            ViewKey = Guard.NotNullOrWhiteSpace(viewKey, "saved_view.view_key_required", "A view key is required."),
            Name = Guard.NotNullOrWhiteSpace(name, "saved_view.name_required", "A name is required."),
            ParametersJson = Guard.NotNullOrWhiteSpace(parametersJson, "saved_view.parameters_required", "Parameters are required."),
            IsShared = isShared,
        };
    }

    /// <summary>Owner edit: rename, replace the parameter payload, and set the shared flag.</summary>
    public void Update(string name, string parametersJson, bool isShared)
    {
        Name = Guard.NotNullOrWhiteSpace(name, "saved_view.name_required", "A name is required.");
        ParametersJson = Guard.NotNullOrWhiteSpace(parametersJson, "saved_view.parameters_required", "Parameters are required.");
        IsShared = isShared;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedBy = deletedBy;
        DeletedAt = deletedAtUtc;
    }
}
