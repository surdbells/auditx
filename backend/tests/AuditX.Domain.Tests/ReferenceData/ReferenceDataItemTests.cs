using AuditX.Domain.Common;
using AuditX.Domain.ReferenceData;

namespace AuditX.Domain.Tests.ReferenceData;

public sealed class ReferenceDataItemTests
{
    [Fact]
    public void Create_trims_and_defaults_to_active()
    {
        var item = ReferenceDataItem.Create("  audit_type  ", "  branch_operations  ", "  Branch Operations  ", "desc", 3);

        Assert.Equal("audit_type", item.Category);
        Assert.Equal("branch_operations", item.Code);
        Assert.Equal("Branch Operations", item.Label);
        Assert.Equal("desc", item.Description);
        Assert.Equal(3, item.SortOrder);
        Assert.True(item.IsActive);
        Assert.False(item.IsDeleted);
    }

    [Theory]
    [InlineData("", "code", "label")]
    [InlineData("audit_type", " ", "label")]
    [InlineData("audit_type", "code", "")]
    public void Create_rejects_blank_required_fields(string category, string code, string label)
        => Assert.Throws<DomainException>(() => ReferenceDataItem.Create(category, code, label, null, 0));

    [Fact]
    public void Update_changes_label_description_and_sort_order_but_not_the_keys()
    {
        var item = ReferenceDataItem.Create("audit_type", "treasury", "Treasury", null, 1);

        item.Update("  Treasury Audit  ", "Covers treasury operations", 5);

        Assert.Equal("Treasury Audit", item.Label);
        Assert.Equal("Covers treasury operations", item.Description);
        Assert.Equal(5, item.SortOrder);
        Assert.Equal("audit_type", item.Category);
        Assert.Equal("treasury", item.Code);
    }

    [Fact]
    public void Update_rejects_a_blank_label()
    {
        var item = ReferenceDataItem.Create("audit_type", "treasury", "Treasury", null, 1);

        Assert.Throws<DomainException>(() => item.Update("   ", null, 0));
    }

    [Fact]
    public void Deactivate_then_activate_toggles_is_active()
    {
        var item = ReferenceDataItem.Create("exception_category", "credit", "Credit", null, 0);

        item.Deactivate();
        Assert.False(item.IsActive);

        item.Activate();
        Assert.True(item.IsActive);
    }

    [Fact]
    public void SoftDelete_sets_the_soft_delete_fields()
    {
        var item = ReferenceDataItem.Create("exception_category", "credit", "Credit", null, 0);
        var who = Guid.NewGuid();
        var when = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        item.SoftDelete(who, when);

        Assert.True(item.IsDeleted);
        Assert.Equal(who, item.DeletedBy);
        Assert.Equal(when, item.DeletedAt);
    }
}

public sealed class ReferenceDataCategoriesTests
{
    [Fact]
    public void All_carries_the_two_seeded_categories_with_labels()
    {
        Assert.Contains(ReferenceDataCategories.All, c => c is { Code: "audit_type", Label: "Audit types" });
        Assert.Contains(ReferenceDataCategories.All, c => c is { Code: "exception_category", Label: "Exception categories" });
    }

    [Fact]
    public void IsKnown_recognises_seeded_categories_only()
    {
        Assert.True(ReferenceDataCategories.IsKnown(ReferenceDataCategories.AuditType));
        Assert.True(ReferenceDataCategories.IsKnown(ReferenceDataCategories.ExceptionCategory));
        Assert.False(ReferenceDataCategories.IsKnown("not_a_category"));
    }
}
