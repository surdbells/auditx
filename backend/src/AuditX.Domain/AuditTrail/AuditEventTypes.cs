namespace AuditX.Domain.AuditTrail;

/// <summary>Canonical audit-trail event-type identifiers for M1 (identity) events.</summary>
public static class AuditEventTypes
{
    // Authentication & session
    public const string UserProvisioned = "user_provisioned";
    public const string LoginSucceeded = "login_success";
    public const string LoginFailed = "login_failure";
    public const string LoggedOut = "logout";
    public const string SessionTerminated = "session_terminated";

    // User lifecycle
    public const string UserDeactivated = "user_deactivated";
    public const string UserReactivated = "user_reactivated";
    public const string NotificationPreferencesUpdated = "notification_preferences_updated";

    // Roles & permissions
    public const string RoleCreated = "role_created";
    public const string RoleUpdated = "role_updated";
    public const string RoleArchived = "role_archived";
    public const string RoleUnarchived = "role_unarchived";
    public const string RoleGranted = "role_granted";
    public const string RoleRevoked = "role_revoked";

    // Delegation
    public const string DelegationStarted = "delegation_started";
    public const string DelegationEnded = "delegation_ended";
    public const string DelegationRevoked = "delegation_revoked";

    // Maker-checker
    public const string MakerCheckerSubmitted = "maker_checker_submitted";
    public const string MakerCheckerApproved = "maker_checker_approved";
    public const string MakerCheckerRejected = "maker_checker_rejected";

    // M2 templates
    public const string TemplateCreated = "template_created";
    public const string TemplateUpdated = "template_updated";
    public const string TemplateItemAdded = "template_item_added";
    public const string TemplateItemEdited = "template_item_edited";
    public const string TemplateItemRemoved = "template_item_removed";
    public const string TemplateItemsReordered = "template_items_reordered";
    public const string TemplateSectionAdded = "template_section_added";
    public const string TemplateSectionRenamed = "template_section_renamed";
    public const string TemplateSectionRemoved = "template_section_removed";
    public const string TemplatePublished = "template_published";
    public const string TemplateDraftCreated = "template_draft_created";
    public const string TemplateArchived = "template_archived";
    public const string TemplateUnarchived = "template_unarchived";
    public const string TemplateCloned = "template_cloned";

    // M14 integrations
    public const string IntegrationConfigured = "integration_configured";
    public const string IntegrationUpdated = "integration_updated";
    public const string IntegrationDeactivated = "integration_deactivated";
    public const string IntegrationTested = "integration_tested";
    public const string IntegrationHealthDegraded = "integration_health_failing";
    public const string WebhookSubscribed = "webhook_subscribed";
    public const string WebhookUnsubscribed = "webhook_unsubscribed";
    public const string WebhookDeadLettered = "webhook_dead_lettered";
    public const string WebhookRetried = "webhook_retried";

    // M15 administration
    public const string BankSettingsUpdated = "bank_settings_updated";
    public const string ResourceLimitsUpdated = "resource_limits_updated";
    public const string UsersBulkDeactivated = "users_bulk_deactivated";
    public const string UsersBulkImported = "users_bulk_imported";
    public const string SupportChannelEnabled = "support_channel_enabled";
    public const string SupportChannelRevoked = "support_channel_revoked";
    public const string ReleaseInstalled = "release_installed";
    public const string ReleaseRejected = "release_rejected";
    public const string RestoreDrillRecorded = "restore_drill_recorded";
    public const string ObjectRestoreRequested = "object_restore_requested";
    public const string ObjectRestoreDecided = "object_restore_decided";

    // M3 universe & planning
    public const string EntityCreated = "universe_entity_created";
    public const string EntityUpdated = "universe_entity_updated";
    public const string EntityArchived = "universe_entity_archived";
    public const string EntityBulkImported = "universe_entities_bulk_imported";
    public const string RiskScoreUpdated = "risk_score_updated";
    public const string EntityLastAuditedUpdated = "entity_last_audited_updated";
    public const string EntityTypeAdded = "universe_entity_type_added";
    public const string EntityTypeRemoved = "universe_entity_type_removed";
    public const string RiskDimensionConfigured = "risk_dimension_configured";
    public const string PlanCreated = "plan_created";
    public const string PlanUpdated = "plan_updated";
    public const string PlanItemAdded = "plan_item_added";
    public const string PlanItemRemoved = "plan_item_removed";
    public const string PlanSubmitted = "plan_submitted";
    public const string PlanRevisionSubmitted = "plan_revision_submitted";
    public const string PlanDecisionRecorded = "plan_decision_recorded";
    public const string PlanClosed = "plan_closed";
    public const string PlanItemLinkedToAudit = "plan_item_linked_to_audit";
}

/// <summary>Canonical target-object-type identifiers used in audit-trail entries.</summary>
public static class AuditTargetTypes
{
    public const string User = "user";
    public const string Role = "role";
    public const string UserRole = "user_role";
    public const string MakerCheckerAction = "maker_checker_action";
    public const string Session = "session";
    public const string BankSettings = "bank_settings";
    public const string Template = "template";
    public const string Integration = "integration";
    public const string WebhookSubscription = "webhook_subscription";
    public const string WebhookDelivery = "webhook_delivery";
    public const string SupportChannel = "support_channel";
    public const string Release = "release";
    public const string RestoreDrill = "restore_drill";
    public const string ObjectRestore = "object_restore";
    public const string AuditUniverseEntity = "audit_universe_entity";
    public const string RiskDimension = "risk_dimension";
    public const string EntityTypeTaxonomy = "entity_type_taxonomy";
    public const string AnnualPlan = "annual_plan";
    public const string PlanItem = "plan_item";
}
