using AuditX.Domain.Enums;
using AuditX.Domain.Notifications;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class NotificationRuleConfiguration : IEntityTypeConfiguration<NotificationRule>
{
    public void Configure(EntityTypeBuilder<NotificationRule> builder)
    {
        builder.ToTable("notification_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.EventType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.RecipientResolutionJson).IsRequired();
        builder.Property(r => r.ChannelsJson).IsRequired();
        builder.Property(r => r.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasIndex(r => new { r.EventType, r.IsActive });
    }
}

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Channel).HasConversion(new SnakeCaseEnumConverter<NotificationChannel>()).HasMaxLength(10).IsRequired();
        builder.Property(t => t.Scope).HasConversion(new SnakeCaseEnumConverter<TemplateScope>()).HasMaxLength(10).IsRequired();
        builder.Property(t => t.SubjectTemplate);
        builder.Property(t => t.BodyTemplate).IsRequired();
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasIndex(t => new { t.TemplateKey, t.Channel, t.Scope }).IsUnique();
    }
}

public sealed class NotificationDispatchConfiguration : IEntityTypeConfiguration<NotificationDispatch>
{
    public void Configure(EntityTypeBuilder<NotificationDispatch> builder)
    {
        builder.ToTable("notification_dispatches");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.EventType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.RecipientAddress).HasMaxLength(320).IsRequired();
        builder.Property(d => d.TemplateKey).HasMaxLength(100).IsRequired();
        builder.Property(d => d.RenderedSubject);
        builder.Property(d => d.RenderedBody).IsRequired();
        builder.Property(d => d.ProviderMessageId).HasMaxLength(256);
        builder.Property(d => d.ProviderResponseJson);
        builder.Property(d => d.LastError);
        builder.Property(d => d.Severity).HasMaxLength(30);
        builder.Property(d => d.Channel).HasConversion(new SnakeCaseEnumConverter<NotificationChannel>()).HasMaxLength(10).IsRequired();
        builder.Property(d => d.Status).HasConversion(new SnakeCaseEnumConverter<DispatchStatus>()).HasMaxLength(20).IsRequired();
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasIndex(d => new { d.Status, d.NextRetryAt });
        builder.HasIndex(d => d.EventId);

        // Back the dispatch-list filters (US-M10): a recipient- or event-type-only filter would otherwise scan a
        // fastest-growing table. Trailing Id aligns the index with the keyset cursor (OrderBy Id) so filter+sort
        // are served without a separate sort.
        builder.HasIndex(d => new { d.RecipientUserId, d.Id });
        builder.HasIndex(d => new { d.EventType, d.Id });

        // Idempotency backstop: one dispatch per (event, rule, recipient, channel). Unique so a concurrent
        // ingest race fails the second insert at the DB rather than producing a duplicate notification.
        builder.HasIndex(d => new { d.EventId, d.RuleId, d.RecipientUserId, d.Channel }).IsUnique();
    }
}

