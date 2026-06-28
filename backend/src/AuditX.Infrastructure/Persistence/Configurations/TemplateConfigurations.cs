using AuditX.Domain.Enums;
using AuditX.Domain.Templates;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.ToTable("templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.AuditType).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.Status)
            .HasConversion(new SnakeCaseEnumConverter<TemplateStatus>())
            .HasMaxLength(40)
            .IsRequired();

        // Unique among live (non-deleted) templates so a name+audit_type can be recreated after soft-delete.
        builder.HasIndex(t => new { t.Name, t.AuditType }).IsUnique().HasFilter("[is_deleted] = 0");
        builder.HasIndex(t => t.AuditType);
        builder.HasIndex(t => t.Status);

        builder.HasMany(t => t.Items).WithOne().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Sections).WithOne().HasForeignKey(s => s.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Versions).WithOne().HasForeignKey(v => v.TemplateId).OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Sections).HasField("_sections").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(t => t.Versions).HasField("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}

public sealed class TemplateItemConfiguration : IEntityTypeConfiguration<TemplateItem>
{
    public void Configure(EntityTypeBuilder<TemplateItem> builder)
    {
        builder.ToTable("template_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Prompt).HasMaxLength(2000).IsRequired();
        builder.Property(i => i.ReferenceNotes);
        builder.Property(i => i.SectionName).HasMaxLength(200);
        builder.Property(i => i.DefaultAssignmentRuleJson);
        builder.Property(i => i.ResponseType)
            .HasConversion(new SnakeCaseEnumConverter<ResponseType>())
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(i => i.TemplateId);
    }
}

public sealed class TemplateSectionConfiguration : IEntityTypeConfiguration<TemplateSection>
{
    public void Configure(EntityTypeBuilder<TemplateSection> builder)
    {
        builder.ToTable("template_sections");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => s.TemplateId);
    }
}

public sealed class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.ToTable("template_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.ItemsSnapshotJson).IsRequired();
        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();
    }
}
