using AuditX.Domain.Common;
using AuditX.Domain.SavedViews;

namespace AuditX.Domain.Tests.SavedViews;

public sealed class SavedViewTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Create_requires_owner_key_name_and_parameters()
    {
        Assert.Throws<DomainException>(() => SavedView.Create(Guid.Empty, "exceptions", "Mine", "{}", false));
        Assert.Throws<DomainException>(() => SavedView.Create(Owner, "  ", "Mine", "{}", false));
        Assert.Throws<DomainException>(() => SavedView.Create(Owner, "exceptions", "  ", "{}", false));
        Assert.Throws<DomainException>(() => SavedView.Create(Owner, "exceptions", "Mine", "  ", false));

        var view = SavedView.Create(Owner, "exceptions", "Critical open", "{\"severity\":\"critical\"}", isShared: true);
        Assert.Equal(Owner, view.OwnerUserId);
        Assert.Equal("exceptions", view.ViewKey);
        Assert.True(view.IsShared);
        Assert.False(view.IsDeleted);
    }

    [Fact]
    public void Update_replaces_name_parameters_and_shared_flag()
    {
        var view = SavedView.Create(Owner, "exceptions", "Mine", "{}", false);
        view.Update("Renamed", "{\"status\":\"open\"}", isShared: true);
        Assert.Equal("Renamed", view.Name);
        Assert.Equal("{\"status\":\"open\"}", view.ParametersJson);
        Assert.True(view.IsShared);
        Assert.Throws<DomainException>(() => view.Update("", "{}", false));
    }

    [Fact]
    public void Soft_delete_is_idempotent()
    {
        var view = SavedView.Create(Owner, "exceptions", "Mine", "{}", false);
        var now = DateTimeOffset.UnixEpoch;
        view.SoftDelete(Owner, now);
        Assert.True(view.IsDeleted);
        Assert.Equal(now, view.DeletedAt);

        // A second delete must not overwrite the original deletion metadata.
        view.SoftDelete(Guid.NewGuid(), now.AddDays(1));
        Assert.Equal(Owner, view.DeletedBy);
        Assert.Equal(now, view.DeletedAt);
    }
}
