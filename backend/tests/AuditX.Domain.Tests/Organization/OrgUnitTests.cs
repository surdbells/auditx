using AuditX.Domain.Common;
using AuditX.Domain.Organization;

namespace AuditX.Domain.Tests.Organization;

public sealed class OrgUnitTests
{
    [Fact]
    public void Create_upper_cases_the_code_and_sets_fields()
    {
        var o = OrgUnit.Create("Retail Banking", " retail ", null);

        Assert.Equal("Retail Banking", o.Name);
        Assert.Equal("RETAIL", o.Code);
        Assert.Null(o.ParentOrgUnitId);
        Assert.False(o.IsArchived);
        Assert.NotEqual(Guid.Empty, o.Id);
    }

    [Fact]
    public void Create_requires_a_name_and_code()
    {
        Assert.Throws<DomainException>(() => OrgUnit.Create("  ", "X", null));
        Assert.Throws<DomainException>(() => OrgUnit.Create("Name", "  ", null));
    }

    [Fact]
    public void SetParent_rejects_self_parenting()
    {
        var o = OrgUnit.Create("Unit", "U", null);
        Assert.Throws<DomainException>(() => o.SetParent(o.Id));
    }

    [Fact]
    public void Archive_and_restore_toggle_the_flag()
    {
        var o = OrgUnit.Create("Unit", "U", null);
        o.Archive();
        Assert.True(o.IsArchived);
        o.Restore();
        Assert.False(o.IsArchived);
    }
}
