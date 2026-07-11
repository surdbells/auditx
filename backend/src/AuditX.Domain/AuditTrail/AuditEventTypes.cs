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
    public const string UserCapacityUpdated = "user_capacity_updated";
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
    public const string TemplateSectionsReordered = "template_sections_reordered";
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
    public const string OrgUnitCreated = "org_unit_created";
    public const string OrgUnitUpdated = "org_unit_updated";
    public const string OrgUnitArchived = "org_unit_archived";
    public const string RiskDimensionConfigured = "risk_dimension_configured";
    public const string PlanCreated = "plan_created";
    public const string PlanUpdated = "plan_updated";
    public const string PlanItemAdded = "plan_item_added";
    public const string PlanItemRemoved = "plan_item_removed";
    public const string PlanItemsReordered = "plan_items_reordered";
    public const string PlanSubmitted = "plan_submitted";
    public const string PlanRevisionSubmitted = "plan_revision_submitted";
    public const string PlanDecisionRecorded = "plan_decision_recorded";
    public const string PlanClosed = "plan_closed";
    public const string PlanItemLinkedToAudit = "plan_item_linked_to_audit";

    // M4 audits — lifecycle & team
    public const string AuditCreated = "audit_created";
    public const string AuditMetadataUpdated = "audit_metadata_updated";
    public const string AuditTransitioned = "audit_transitioned";
    public const string AuditCompleted = "audit_completed";
    public const string AuditCancelled = "audit_cancelled";
    public const string AuditTeamMemberAdded = "audit_team_member_added";
    public const string AuditTeamMemberRemoved = "audit_team_member_removed";
    public const string AuditLeadTransferred = "audit_lead_transferred";
    public const string AuditChecklistItemAdded = "audit_checklist_item_added";
    public const string AuditChecklistItemEdited = "audit_checklist_item_edited";
    public const string AuditChecklistItemRemoved = "audit_checklist_item_removed";
    public const string AuditChecklistItemsReordered = "audit_checklist_items_reordered";
    public const string AuditSectionAdded = "audit_section_added";
    public const string AuditSectionRenamed = "audit_section_renamed";
    public const string AuditSectionRemoved = "audit_section_removed";
    public const string AuditSectionsReordered = "audit_sections_reordered";

    // Risk register (P1-A)
    public const string RiskRegistered = "risk_registered";
    public const string RiskUpdated = "risk_updated";
    public const string RiskStatusChanged = "risk_status_changed";
    public const string RiskDeleted = "risk_deleted";

    // Controls & Compliance registers (P1-B)
    public const string ControlRegistered = "control_registered";
    public const string ControlUpdated = "control_updated";
    public const string ControlStatusChanged = "control_status_changed";
    public const string ControlDeleted = "control_deleted";
    public const string RegulationRegistered = "regulation_registered";
    public const string RegulationUpdated = "regulation_updated";
    public const string RegulationStatusChanged = "regulation_status_changed";
    public const string RegulationDeleted = "regulation_deleted";
    public const string FindingLinkAdded = "finding_link_added";
    public const string FindingLinkRemoved = "finding_link_removed";

    // Time tracking (P0-B)
    public const string TimeLogged = "time_logged";
    public const string TimeEntryAmended = "time_entry_amended";
    public const string TimeEntryDeleted = "time_entry_deleted";
    public const string AuditBudgetSet = "audit_budget_set";

    // P2-C execution procedures (sampling / interview / walkthrough)
    public const string ProcedureRecorded = "procedure_recorded";
    public const string ProcedureDeleted = "procedure_deleted";

    // P2-D expected / requested evidence
    public const string EvidenceRequested = "evidence_requested";
    public const string EvidenceReceived = "evidence_received";
    public const string EvidenceWaived = "evidence_waived";
    public const string EvidenceRequestDeleted = "evidence_request_deleted";

    // M5 execution / evidence
    public const string ItemResponded = "item_responded";
    public const string ItemResponseOverridden = "item_response_overridden";
    public const string DraftDiscarded = "draft_discarded";
    public const string ItemAssigned = "item_assigned";
    public const string ItemsBulkReassigned = "items_bulk_reassigned";
    public const string FailJudgementRecorded = "fail_judgement_recorded";
    public const string EvidenceUploaded = "evidence_uploaded";
    public const string EvidenceSoftDeleted = "evidence_soft_deleted";
    public const string EvidenceHashMismatch = "evidence_hash_mismatch";
    public const string EvidenceLocked = "evidence_locked";

    // M6 exceptions & MAP
    public const string ExceptionRaised = "exception_raised";
    public const string FindingRegisterExported = "finding_register_exported";
    public const string ExceptionSeverityChanged = "exception_severity_changed";
    public const string ExceptionOwnerReassigned = "exception_owner_reassigned";
    public const string MapSubmitted = "map_submitted";
    public const string MapApproved = "map_approved";
    public const string MapRejected = "map_rejected";
    public const string MapReturnedForEvidence = "map_returned_for_evidence";
    public const string MapActionCompleted = "map_action_completed";
    public const string MapCompleted = "map_completed";
    public const string ExceptionPendingCia = "exception_pending_cia";
    public const string ExceptionClosed = "exception_closed";
    public const string ExceptionCancelled = "exception_cancelled";
    public const string ManagementResponseRecorded = "management_response_recorded";
    public const string ManagementResponseDueDateSet = "management_response_due_date_set";
    public const string FindingVerified = "finding_verified";
    public const string ExceptionReopened = "exception_reopened";

    // M10 notifications (NotificationPreferencesUpdated already defined above for M1)
    public const string NotificationDispatched = "notification_dispatched";
    public const string NotificationRetried = "notification_retried";
    public const string NotificationRuleConfigured = "notification_rule_configured";
    public const string NotificationTemplateOverridden = "notification_template_overridden";

    // M11 audit trail & evidence integrity
    public const string TrailQueried = "trail_query";
    public const string TrailExported = "trail_exported";
    public const string EvidenceUnflagged = "evidence_unflagged";

    // M7 sanctions & disciplinary grid
    public const string SanctionsTriggered = "sanctions_triggered";
    public const string SanctionsRecommended = "sanctions_recommended";
    public const string SanctionsRecommendationSubmitted = "sanctions_recommendation_submitted";
    public const string HrOutcomeRecorded = "hr_outcome_recorded";
    public const string DcReferral = "dc_referral";
    public const string DcDecisionRecorded = "dc_decision_recorded";
    public const string AppealFiled = "appeal_filed";
    public const string AppealOutcomeRecorded = "appeal_outcome_recorded";
    public const string SanctionsCaseClosed = "sanctions_case_closed";
    public const string GridVersionCreated = "grid_version_created";
    public const string GridVersionActivated = "grid_version_activated";
    public const string SubjectIdentityExposed = "subject_identity_exposed";

    // M8 reports
    public const string ReportGenerationRequested = "report_generation_requested";
    public const string ReportGenerated = "report_generated";
    public const string ReportGenerationFailed = "report_generation_failed";
    public const string ReportDistributed = "report_distributed";
    public const string ReportDownloaded = "report_downloaded";
    public const string ReportExpired = "report_expired";
    public const string ReportDeliveryOutcomeRecorded = "report_delivery_outcome_recorded";
    public const string ReportShareLinkCreated = "report_share_link_created";
    public const string ReportShareLinkRevoked = "report_share_link_revoked";
    public const string ReportScheduleCreated = "report_schedule_created";
    public const string ReportScheduleUpdated = "report_schedule_updated";
    public const string ReportScheduleDeleted = "report_schedule_deleted";
    public const string ReportScheduleRun = "report_schedule_run";
    public const string ReportHashMismatch = "report_hash_mismatch";
    public const string ReportTemplateCreated = "report_template_created";
    public const string ReportTemplateActivated = "report_template_activated";

    // M9 analytics & dashboards
    public const string DashboardRefreshed = "dashboard_refreshed";
    public const string DashboardWidgetConfigured = "dashboard_widget_configured";
    public const string RecurrenceClusterDetected = "recurrence_cluster_detected";

    // M12 configuration
    public const string ConfigurationVersionCreated = "configuration_version_created";
    public const string ConfigurationVersionActivated = "configuration_version_activated";
    public const string ConfigurationRolledBack = "configuration_rolled_back";

    // Reference data (managed lists)
    public const string ReferenceDataItemCreated = "reference_data_item_created";
    public const string ReferenceDataItemUpdated = "reference_data_item_updated";
    public const string ReferenceDataItemArchived = "reference_data_item_archived";
    public const string ReferenceDataItemReactivated = "reference_data_item_reactivated";

    // M13 audit committee
    public const string AcPackGenerationRequested = "ac_pack_generation_requested";
    public const string AcPackGenerated = "ac_pack_generated";
    public const string AcPackGenerationFailed = "ac_pack_generation_failed";
    public const string AcPackSupplementaryTextUpdated = "ac_pack_supplementary_text_updated";
    public const string AcPackApproved = "ac_pack_approved";
    public const string AcPackDistributed = "ac_pack_distributed";
    public const string AcPackHashMismatch = "ac_pack_hash_mismatch";
    public const string AcActionItemCreated = "ac_action_item_created";
    public const string AcActionItemUpdated = "ac_action_item_updated";
    public const string AcActionItemClosureAcknowledged = "ac_action_item_closure_acknowledged";
    public const string AcCommentAdded = "ac_comment_added";
    public const string FindingVisibilityRestricted = "finding_visibility_restricted";
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
    public const string OrgUnit = "org_unit";
    public const string RiskDimension = "risk_dimension";
    public const string EntityTypeTaxonomy = "entity_type_taxonomy";
    public const string AnnualPlan = "annual_plan";
    public const string PlanItem = "plan_item";
    public const string Audit = "audit";
    public const string AuditTeamMember = "audit_team_member";
    public const string AuditSection = "audit_section";
    public const string AuditChecklistItem = "audit_checklist_item";
    public const string ChecklistResponse = "checklist_response";
    public const string TimeEntry = "time_entry";
    public const string AuditProcedure = "audit_procedure";
    public const string EvidenceRequest = "evidence_request";
    public const string Risk = "risk";
    public const string Control = "control";
    public const string Regulation = "regulation";
    public const string FindingLink = "finding_link";
    public const string EvidenceFile = "evidence_file";
    public const string Exception = "exception";
    public const string MapAction = "map_action";
    public const string FindingVerification = "finding_verification";
    public const string NotificationDispatch = "notification_dispatch";
    public const string NotificationRule = "notification_rule";
    public const string NotificationTemplate = "notification_template";
    public const string AuditTrail = "audit_trail";
    public const string SanctionsCase = "sanctions_case";
    public const string SanctionsAppeal = "sanctions_appeal";
    public const string SanctionsGridVersion = "sanctions_grid_version";
    public const string Report = "report";
    public const string ReportTemplate = "report_template";
    public const string SharedLink = "shared_link";
    public const string ReportSchedule = "report_schedule";
    public const string Dashboard = "dashboard";
    public const string RecurrenceCluster = "recurrence_cluster";
    public const string BankConfiguration = "bank_configuration";
    public const string ReferenceDataItem = "reference_data_item";
    public const string AcPack = "ac_pack";
    public const string AcActionItem = "ac_action_item";
    public const string AcComment = "ac_comment";
    public const string FindingVisibilityRestriction = "finding_visibility_restriction";
}
