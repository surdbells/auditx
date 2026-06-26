using AuditX.Domain.Administration;
using AuditX.Domain.Enums;
using AuditX.Domain.Integrations;
using AuditX.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditX.Infrastructure.Persistence.Configurations;

public sealed class IntegrationConfigurationConfiguration : IEntityTypeConfiguration<IntegrationConfiguration>
{
    public void Configure(EntityTypeBuilder<IntegrationConfiguration> builder)
    {
        builder.ToTable("integration_configurations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ConnectionDetailsJson).IsRequired();
        builder.Property(i => i.EncryptedCredentials);
        builder.Property(i => i.Type).HasConversion(new SnakeCaseEnumConverter<IntegrationType>()).HasMaxLength(40).IsRequired();

        builder.HasIndex(i => i.Type);
        builder.HasIndex(i => i.IsActive);
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.DestinationUrl).HasMaxLength(2048).IsRequired();
        builder.Property(s => s.EncryptedHmacSecret).IsRequired();
        builder.Property(s => s.RetryPolicyJson);
        builder.PrimitiveCollection(s => s.SubscribedEventTypes).HasColumnName("subscribed_event_types");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.EventType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PayloadJson).IsRequired();
        builder.Property(d => d.LastError);
        builder.Property(d => d.Status).HasConversion(new SnakeCaseEnumConverter<WebhookDeliveryStatus>()).HasMaxLength(40).IsRequired();

        builder.HasIndex(d => d.SubscriptionId);
        builder.HasIndex(d => new { d.Status, d.NextRetryAt });
    }
}

public sealed class IntegrationHealthStatusConfiguration : IEntityTypeConfiguration<IntegrationHealthStatus>
{
    public void Configure(EntityTypeBuilder<IntegrationHealthStatus> builder)
    {
        builder.ToTable("integration_health_status");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.State).HasConversion(new SnakeCaseEnumConverter<IntegrationHealthState>()).HasMaxLength(40).IsRequired();
        builder.HasIndex(h => h.IntegrationId).IsUnique();
    }
}

public sealed class SupportChannelSessionConfiguration : IEntityTypeConfiguration<SupportChannelSession>
{
    public void Configure(EntityTypeBuilder<SupportChannelSession> builder)
    {
        builder.ToTable("support_channel_sessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.PrimitiveCollection(s => s.EngineerIdentifiers).HasColumnName("engineer_identifiers");
        builder.HasIndex(s => s.ExpiresAt);
    }
}

public sealed class ReleaseInstallConfiguration : IEntityTypeConfiguration<ReleaseInstall>
{
    public void Configure(EntityTypeBuilder<ReleaseInstall> builder)
    {
        builder.ToTable("release_installs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Version).HasMaxLength(100).IsRequired();
        builder.Property(r => r.ManifestSha256).HasMaxLength(128).IsRequired();
        builder.Property(r => r.ChangeRecordReference).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Detail).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion(new SnakeCaseEnumConverter<ReleaseInstallStatus>()).HasMaxLength(40).IsRequired();
    }
}

public sealed class RestoreDrillConfiguration : IEntityTypeConfiguration<RestoreDrill>
{
    public void Configure(EntityTypeBuilder<RestoreDrill> builder)
    {
        builder.ToTable("restore_drills");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Details).HasMaxLength(2000);
        builder.Property(d => d.Outcome).HasConversion(new SnakeCaseEnumConverter<RestoreOutcome>()).HasMaxLength(40).IsRequired();
    }
}

public sealed class ObjectRestoreRequestConfiguration : IEntityTypeConfiguration<ObjectRestoreRequest>
{
    public void Configure(EntityTypeBuilder<ObjectRestoreRequest> builder)
    {
        builder.ToTable("object_restore_requests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.ObjectType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Justification).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.DecisionComment).HasMaxLength(2000);
        builder.Property(r => r.Status).HasConversion(new SnakeCaseEnumConverter<ObjectRestoreStatus>()).HasMaxLength(40).IsRequired();
    }
}
