namespace AuditX.Api.Contracts;

public sealed record CreateConfigurationDraftRequest(string DefinitionJson, string ChangeReason);

public sealed record ActivateConfigurationRequest(string ChangeReason);

public sealed record RollbackConfigurationRequest(int ToVersionNumber, string ChangeReason);
