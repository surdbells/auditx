using AuditX.Domain.Common;
using AuditX.Domain.Universe;

namespace AuditX.Domain.Tests.Universe;

/// <summary>Archiving an entity only hides it — restoring is an exact undo.</summary>
public sealed class AuditableEntityArchiveTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    private static AuditableEntity NewEntity()
        => AuditableEntity.Create("branch", "Ikeja Branch", "Flagship branch", parentEntityId: null, ownerUserId: Guid.NewGuid());

    [Fact]
    public void Archiving_marks_the_entity_archived_with_who_and_when()
    {
        var entity = NewEntity();
        var by = Guid.NewGuid();

        entity.SoftDelete(by, Now);

        Assert.True(entity.IsDeleted);
        Assert.Equal(by, entity.DeletedBy);
        Assert.Equal(Now, entity.DeletedAt);
    }

    [Fact]
    public void Restoring_clears_the_archive_and_keeps_everything_else()
    {
        var entity = NewEntity();
        var owner = entity.OwnerUserId;
        entity.SetExpectedAuditsPerYear(2);
        entity.SoftDelete(Guid.NewGuid(), Now);

        entity.Restore();

        Assert.False(entity.IsDeleted);
        Assert.Null(entity.DeletedAt);
        Assert.Null(entity.DeletedBy);
        // Nothing about the entity itself was lost by the round trip.
        Assert.Equal("Ikeja Branch", entity.Name);
        Assert.Equal("Flagship branch", entity.Description);
        Assert.Equal(owner, entity.OwnerUserId);
        Assert.Equal(2, entity.ExpectedAuditsPerYear);
    }

    [Fact]
    public void An_entity_that_is_not_archived_cannot_be_restored()
    {
        var ex = Assert.Throws<DomainException>(() => NewEntity().Restore());

        Assert.Equal("universe.not_archived", ex.Code);
    }

    [Fact]
    public void An_entity_can_be_archived_and_restored_repeatedly()
    {
        var entity = NewEntity();

        entity.SoftDelete(null, Now);
        entity.Restore();
        entity.SoftDelete(null, Now.AddDays(1));

        Assert.True(entity.IsDeleted);
        Assert.Equal(Now.AddDays(1), entity.DeletedAt);
    }
}
