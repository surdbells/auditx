using System.Linq;
using System.Text.Json;
using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Templates;

namespace AuditX.Domain.Tests.Templates;

public sealed class TemplateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static string Serialize(IReadOnlyList<TemplateItemSnapshot> snapshot) => JsonSerializer.Serialize(snapshot);

    private static Template DraftWithItem()
    {
        var template = Template.CreateDraft("Branch Audit", "branch", "desc");
        template.AddItem("Cash counted daily?", null, ResponseType.PassFailNa, null, isRequired: true, null);
        return template;
    }

    [Fact]
    public void CreateDraft_starts_in_draft_at_version_1()
    {
        var template = Template.CreateDraft("T", "branch", null);
        Assert.Equal(TemplateStatus.Draft, template.Status);
        Assert.Equal(1, template.CurrentVersion);
    }

    [Fact]
    public void Cannot_publish_an_empty_template()
    {
        var template = Template.CreateDraft("T", "branch", null);
        Assert.Throws<DomainException>(() => template.Publish(Now, Serialize));
    }

    [Fact]
    public void Publish_snapshots_items_and_marks_published()
    {
        var template = DraftWithItem();

        var version = template.Publish(Now, Serialize);

        Assert.Equal(TemplateStatus.Published, template.Status);
        Assert.Equal(1, version.VersionNumber);
        Assert.Single(template.Versions);
        Assert.Contains("Cash counted daily?", version.ItemsSnapshotJson);
    }

    [Fact]
    public void Published_template_rejects_in_place_edits()
    {
        var template = DraftWithItem();
        template.Publish(Now, Serialize);

        Assert.Throws<InvalidStateTransitionException>(() =>
            template.AddItem("New", null, ResponseType.PassFailNa, null, false, null));
    }

    [Fact]
    public void New_draft_increments_version_and_returns_to_draft()
    {
        var template = DraftWithItem();
        template.Publish(Now, Serialize);

        template.CreateNewDraft();

        Assert.Equal(TemplateStatus.Draft, template.Status);
        Assert.Equal(2, template.CurrentVersion);
        // Editing is allowed again.
        template.AddItem("Second item", null, ResponseType.PassFailNa, null, false, null);
        Assert.Equal(2, template.Items.Count);
    }

    [Fact]
    public void Second_publish_creates_version_2()
    {
        var template = DraftWithItem();
        template.Publish(Now, Serialize);
        template.CreateNewDraft();

        var v2 = template.Publish(Now, Serialize);

        Assert.Equal(2, v2.VersionNumber);
        Assert.Equal(2, template.Versions.Count);
    }

    [Fact]
    public void Section_in_use_cannot_be_removed()
    {
        var template = Template.CreateDraft("T", "branch", null);
        template.AddSection("Cash");
        template.AddItem("Q", null, ResponseType.PassFailNa, "Cash", false, null);

        Assert.Throws<DomainException>(() => template.RemoveSection("Cash"));
    }

    [Fact]
    public void Adding_item_to_unknown_section_is_rejected()
    {
        var template = Template.CreateDraft("T", "branch", null);
        Assert.Throws<DomainException>(() =>
            template.AddItem("Q", null, ResponseType.PassFailNa, "Nonexistent", false, null));
    }

    [Fact]
    public void Reorder_must_list_every_item_once()
    {
        var template = Template.CreateDraft("T", "branch", null);
        var a = template.AddItem("A", null, ResponseType.PassFailNa, null, false, null);
        template.AddItem("B", null, ResponseType.PassFailNa, null, false, null);

        Assert.Throws<DomainException>(() => template.ReorderItems([a.Id]));
    }

    [Fact]
    public void ReorderSections_sets_order_by_position()
    {
        var template = Template.CreateDraft("T", "branch", null);
        template.AddSection("A");
        template.AddSection("B");
        template.AddSection("C");

        template.ReorderSections(["C", "A", "B"]);

        var ordered = template.Sections.OrderBy(s => s.OrderIndex).Select(s => s.Name).ToArray();
        Assert.Equal(["C", "A", "B"], ordered);
    }

    [Fact]
    public void ReorderSections_must_list_every_section_once()
    {
        var template = Template.CreateDraft("T", "branch", null);
        template.AddSection("A");
        template.AddSection("B");

        Assert.Throws<DomainException>(() => template.ReorderSections(["A"]));
    }

    [Fact]
    public void Archive_then_unarchive_restores_published_when_versions_exist()
    {
        var template = DraftWithItem();
        template.Publish(Now, Serialize);
        template.Archive();
        Assert.Equal(TemplateStatus.Archived, template.Status);

        template.Unarchive();
        Assert.Equal(TemplateStatus.Published, template.Status);
    }
}
